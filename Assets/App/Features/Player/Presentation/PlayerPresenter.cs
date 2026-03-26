using System;
using R3;
using FloorBreaker.Shared.Domain.Grid;
using FloorBreaker.Shared.Domain.Primitives;
using FloorBreaker.Shared.Presentation.Common;
using FloorBreaker.Player.Domain;

namespace FloorBreaker.Player.Presentation
{
    /// <summary>
    /// PlayerModel の R3 購読 → PlayerView / PlayerAnimationService へのディスパッチ。
    /// pure C# クラス (MonoBehaviour ではない)。
    /// </summary>
    public sealed class PlayerPresenter : IDisposable
    {
        private readonly PlayerModel _model;
        private readonly PlayerView _view;
        private readonly PlayerAnimationService _animService;
        private readonly PlayerSpriteConfig _config;
        private readonly CompositeDisposable _subscriptions = new();

        // Walk animation state
        private float _walkTimer;
        private bool _isWalkFrame;
        private bool _isMoving;
        private float _moveEndTimer;

        // Invulnerability edge detection
        private bool _wasInvulnerable;

        // Death guard
        private bool _isDead;

        public PlayerPresenter(
            PlayerModel model,
            PlayerView view,
            PlayerAnimationService animService,
            PlayerSpriteConfig config)
        {
            _model = model;
            _view = view;
            _animService = animService;
            _config = config;

            // Position change → movement animation
            model.Position.Subscribe(OnPositionChanged).AddTo(_subscriptions);

            // Facing direction → sprite update
            model.FacingDirection.Subscribe(OnFacingChanged).AddTo(_subscriptions);

            // HP change → damage flash / death
            model.Stats.CurrentHp.Pairwise().Subscribe(OnHpChanged).AddTo(_subscriptions);
        }

        private void OnPositionChanged(GridPos pos)
        {
            if (_isDead) return;
            var worldPos = pos.ToWorldCenter().ToVector3(-1f);

            if (_model.ForcedMove.IsForced)
            {
                _animService.PlayForcedMove(_view, worldPos);
                _moveEndTimer = _config.ForcedMoveDuration;
            }
            else
            {
                _animService.PlayMove(_view, worldPos);
                _moveEndTimer = _config.MoveDuration;
            }
            _isMoving = true;
            _walkTimer = 0f;
        }

        private void OnFacingChanged(Direction8 dir)
        {
            if (_isDead) return;
            _view.SetDirection(dir, _config);
        }

        private void OnHpChanged((int Previous, int Current) pair)
        {
            if (pair.Current < pair.Previous && !_isDead)
            {
                _animService.PlayHitFlash(_view);
            }
            if (pair.Current <= 0 && !_isDead)
            {
                _isDead = true;
                _animService.PlayDeath(_view);
            }
        }

        /// <summary>
        /// 毎フレーム呼び出す。歩行フレームトグルと無敵ビジュアルのエッジ検出を処理。
        /// </summary>
        public void Tick(float deltaTime)
        {
            if (_isDead) return;

            // Walk frame toggle
            if (_isMoving)
            {
                _moveEndTimer -= deltaTime;
                _walkTimer += deltaTime;
                if (_walkTimer >= _config.WalkFrameInterval)
                {
                    _walkTimer -= _config.WalkFrameInterval;
                    _isWalkFrame = !_isWalkFrame;
                    _view.SetWalkFrame(_isWalkFrame, _config);
                }
                if (_moveEndTimer <= 0f)
                {
                    _isMoving = false;
                    _isWalkFrame = false;
                    _view.SetWalkFrame(false, _config);
                }
            }

            // Invulnerability blink edge detection
            bool isInvul = _model.Invulnerability.IsInvulnerable;
            if (!_wasInvulnerable && isInvul)
            {
                _animService.StartInvulnerabilityBlink(_view);
            }
            else if (_wasInvulnerable && !isInvul)
            {
                _animService.StopInvulnerabilityBlink(_view, _config.GetPlayerTint(_model.Id));
            }
            _wasInvulnerable = isInvul;
        }

        public void Dispose()
        {
            _subscriptions.Dispose();
        }
    }
}
