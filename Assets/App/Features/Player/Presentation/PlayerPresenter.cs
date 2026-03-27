using System;
using R3;
using FloorBreaker.Shared.Domain.Grid;
using FloorBreaker.Shared.Domain.Primitives;
using FloorBreaker.Shared.Application.Interfaces;
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
        private readonly IAudioService _audio;
        private readonly ICameraShakeService _cameraShake;
        private readonly IImpactFreezeService _impactFreeze;
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

        // Dispose guard (シーン遷移時の View 破棄後アクセス防止)
        private bool _disposed;

        public PlayerPresenter(
            PlayerModel model,
            PlayerView view,
            PlayerAnimationService animService,
            PlayerSpriteConfig config,
            IAudioService audio = null,
            ICameraShakeService cameraShake = null,
            IImpactFreezeService impactFreeze = null)
        {
            _model = model;
            _view = view;
            _animService = animService;
            _config = config;
            _audio = audio;
            _cameraShake = cameraShake;
            _impactFreeze = impactFreeze;

            // Position change → movement animation
            model.Position.Subscribe(OnPositionChanged).AddTo(_subscriptions);

            // Facing direction → sprite update
            model.FacingDirection.Subscribe(OnFacingChanged).AddTo(_subscriptions);

            // HP change → damage flash / death
            model.Stats.CurrentHp.Pairwise().Subscribe(OnHpChanged).AddTo(_subscriptions);
        }

        private void OnPositionChanged(GridPos pos)
        {
            if (_disposed || _isDead) return;
            var worldPos = pos.ToWorldCenter().ToVector3(-1f);

            if (_model.ForcedMove.IsForced)
            {
                _animService.PlayForcedMove(_view, worldPos);
                _moveEndTimer = _config.ForcedMoveDuration;
            }
            else
            {
                float speed = _model.Stats.MoveSpeed;
                if (speed <= 0f) speed = 0.1f;
                float moveDuration = _config.BaseMoveInterval / speed;
                _animService.PlayMove(_view, worldPos, moveDuration);
                _moveEndTimer = moveDuration;
            }
            _isMoving = true;
            _walkTimer = 0f;
        }

        private void OnFacingChanged(Direction8 dir)
        {
            if (_disposed || _isDead) return;
            _view.SetDirection(dir, _config);
        }

        private void OnHpChanged((int Previous, int Current) pair)
        {
            if (_disposed) return;
            if (pair.Current < pair.Previous && !_isDead)
            {
                _animService.PlayHitFlash(_view);
                _audio?.PlaySfx(SfxIds.PlayerHit);
            }
            if (pair.Current <= 0 && !_isDead)
            {
                _isDead = true;
                _animService.PlayDeath(_view);
                _audio?.PlaySfx(SfxIds.PlayerDeath);
                _cameraShake?.Shake(ShakeIntensity.Medium);
                _impactFreeze?.PlayImpact(ImpactLevel.Heavy);
            }
        }

        /// <summary>
        /// 毎フレーム呼び出す。歩行フレームトグルと無敵ビジュアルのエッジ検出を処理。
        /// </summary>
        public void Tick(float deltaTime)
        {
            if (_disposed || _isDead) return;

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
            _disposed = true;
            _subscriptions.Dispose();
        }
    }
}
