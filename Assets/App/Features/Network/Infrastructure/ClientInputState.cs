using FloorBreaker.Shared.Domain.Grid;
using FloorBreaker.Shared.Domain.Primitives;

namespace FloorBreaker.Network.Infrastructure
{
    /// <summary>
    /// クライアント側の入力蓄積状態。
    /// PlayerInputAdapter のイベントから入力を受け取り、Fusion の OnInput() ごとに
    /// FloorBreakerInput を生成する。
    /// GameplayInputBridge.MoveRepeatState と同等のホールドリピートロジックを持つ。
    /// </summary>
    public sealed class ClientInputState
    {
        // --- ホールドリピート移動 ---
        private bool _isHolding;
        private Direction8 _heldDirection;
        private float _timer;
        private int _repeatCount;
        private bool _firstMoveDone;
        private float _cooldownRemaining;

        // --- ボム（ワンショット） ---
        private bool _breakBombPressed;
        private bool _breakBombReleased;
        private bool _fireBombPressed;
        private bool _fireBombReleased;

        // --- ダッシュ（ワンショット） ---
        private bool _dashTriggered;
        private Direction8 _dashDirection;

        // --- 強化フェーズ（ワンショット） ---
        private UpgradeInputAction _upgradeAction;

        // --- バランスパラメータ ---
        private readonly float _bufferTime;
        private readonly float _initialRepeatDelay;
        private readonly float _baseMoveInterval;

        public ClientInputState(float bufferTime, float initialRepeatDelay, float baseMoveInterval)
        {
            _bufferTime = bufferTime;
            _initialRepeatDelay = initialRepeatDelay;
            _baseMoveInterval = baseMoveInterval;
        }

        // =============================================
        // PlayerInputAdapter イベントハンドラ
        // =============================================

        public void OnMovePressed(Direction8 direction)
        {
            if (!_isHolding)
            {
                _isHolding = true;
                _heldDirection = direction;
                _timer = 0f;
                _repeatCount = 0;
                _firstMoveDone = false;
            }
        }

        public void OnMoveReleased()
        {
            _isHolding = false;
            _timer = 0f;
            _repeatCount = 0;
            _firstMoveDone = false;
            // CooldownRemaining ���リセットしない（連打防止、GameplayInputBridge と同一動作）
        }

        /// <summary>ホールド中の方向を更新する（アダプターの HeldDirection 反映）。</summary>
        public void UpdateHeldDirection(Direction8 direction)
        {
            if (_isHolding)
                _heldDirection = direction;
        }

        public void OnBombHold(BombType type, bool pressed)
        {
            if (type == BombType.Break)
            {
                if (pressed) _breakBombPressed = true;
                else _breakBombReleased = true;
            }
            else
            {
                if (pressed) _fireBombPressed = true;
                else _fireBombReleased = true;
            }
        }

        public void OnDashTriggered(Direction8 direction)
        {
            _dashTriggered = true;
            _dashDirection = direction;
        }

        public void SetUpgradeAction(UpgradeInputAction action)
        {
            if (action != UpgradeInputAction.None)
                _upgradeAction = action;
        }

        // =============================================
        // Poll — Fusion OnInput() から毎Tick呼ばれる
        // =============================================

        /// <summary>
        /// リピートタイマーを進め、FloorBreakerInput を生成する。
        /// ワンショットフラグは消費後にクリアされる。
        /// </summary>
        /// <param name="deltaTime">Fusion の固定 Tick 間隔 (Runner.DeltaTime)</param>
        /// <param name="moveSpeed">ローカルプレイヤーの現在移動速度</param>
        public FloorBreakerInput Poll(float deltaTime, float moveSpeed)
        {
            bool moveHeld = false;
            Direction8 moveDir = default;

            // ホールドリピートロジック（GameplayInputBridge.Tick と同等）
            if (_isHolding)
            {
                // クールダウン減算
                if (_cooldownRemaining > 0f)
                    _cooldownRemaining -= deltaTime;

                float moveInterval = GetMoveInterval(moveSpeed);

                _timer += deltaTime;

                if (!_firstMoveDone)
                {
                    // 初回移動: バッファ時間経過後
                    if (_timer >= _bufferTime && _cooldownRemaining <= 0f)
                    {
                        moveHeld = true;
                        moveDir = _heldDirection;
                        _cooldownRemaining = moveInterval;
                        _firstMoveDone = true;
                        _timer = 0f;
                        _repeatCount = 0;
                    }
                }
                else
                {
                    // リピート移動
                    float threshold = _repeatCount == 0
                        ? moveInterval + _initialRepeatDelay
                        : moveInterval;

                    if (_timer >= threshold)
                    {
                        _timer -= threshold;
                        _repeatCount++;
                        moveHeld = true;
                        moveDir = _heldDirection;
                        _cooldownRemaining = moveInterval;
                    }
                }
            }
            else
            {
                // ホールドしていなくてもクールダウンは減算
                if (_cooldownRemaining > 0f)
                    _cooldownRemaining -= deltaTime;
            }

            var input = new FloorBreakerInput
            {
                MoveDirection = moveDir,
                MoveHeld = moveHeld,
                BreakBombPressed = _breakBombPressed,
                BreakBombReleased = _breakBombReleased,
                FireBombPressed = _fireBombPressed,
                FireBombReleased = _fireBombReleased,
                DashTriggered = _dashTriggered,
                DashDirection = _dashDirection,
                UpgradeAction = _upgradeAction,
            };

            // ワンショットフラグをクリア
            _breakBombPressed = false;
            _breakBombReleased = false;
            _fireBombPressed = false;
            _fireBombReleased = false;
            _dashTriggered = false;
            _upgradeAction = UpgradeInputAction.None;

            return input;
        }

        private float GetMoveInterval(float moveSpeed)
        {
            if (moveSpeed <= 0f) moveSpeed = 0.1f;
            return _baseMoveInterval / moveSpeed;
        }
    }
}
