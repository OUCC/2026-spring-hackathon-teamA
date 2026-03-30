using System;
using UnityEngine;
using UnityEngine.InputSystem;
using FloorBreaker.Shared.Domain.Grid;
using FloorBreaker.Shared.Domain.Primitives;
using FloorBreaker.Shared.Application.Interfaces;
using FloorBreaker.Bombs.Domain;

namespace FloorBreaker.Input.Infrastructure
{
    /// <summary>
    /// InputActionAsset から直接アクションマップを取得し、
    /// Domain 型に変換して Application 層に渡す。
    /// PlayerInput コンポーネントは使わない（1キーボード2人対戦対応）。
    /// </summary>
    public sealed class PlayerInputAdapter : MonoBehaviour
    {
        [SerializeField] private InputActionAsset _inputActions;

        private PlayerId _playerId;
        private ITimeProvider _timeProvider;
        private Direction8 _lastDirection = Direction8.S;
        private Direction8? _heldDirection;
        private bool _isAimLocked;
        private InputActionMap _gameplayMap;

        public PlayerId Owner => _playerId;
        public Direction8 LastDirection => _lastDirection;
        public Direction8? HeldDirection => _heldDirection;
        public bool IsAimLocked => _isAimLocked;
        public InputActionAsset InputActions => _inputActions;

        public event Action<PlayerId, Direction8> OnMoveInput;
        public event Action<PlayerId> OnMoveReleased;
        public event Action<BombHoldCommand> OnBombHoldInput;
        public event Action<PlayerId, Direction8> OnDashTriggered;

        // ダブルタップ検出
        private Direction8? _lastTapDirection;
        private float _lastTapTime;
        private float _doubleTapWindow = 0.3f;

        /// <summary>
        /// 特定のデバイスのみに制限する（ゲームパッド割り当て用）。
        /// </summary>
        public void RestrictToDevice(InputDevice device)
        {
            if (_gameplayMap != null && device != null)
                _gameplayMap.devices = new InputDevice[] { device };
        }

        public void Initialize(PlayerId playerId, ITimeProvider timeProvider, InputActionAsset actions = null)
        {
            _playerId = playerId;
            _timeProvider = timeProvider;
            if (actions != null) _inputActions = actions;
            BindActions();
        }

        private void OnDestroy()
        {
            UnbindActions();
        }

        private void BindActions()
        {
            if (_inputActions == null) return;

            string mapName = $"Gameplay_P{_playerId.Index + 1}";
            _gameplayMap = _inputActions.FindActionMap(mapName);
            if (_gameplayMap == null) return;

            _gameplayMap.Enable();

            _gameplayMap["Move"].performed += OnMove;
            _gameplayMap["Move"].canceled += OnMoveCanceled;
            _gameplayMap["BreakBombHold"].started += OnBreakBombStarted;
            _gameplayMap["BreakBombHold"].canceled += OnBreakBombCanceled;
            _gameplayMap["FireBombHold"].started += OnFireBombStarted;
            _gameplayMap["FireBombHold"].canceled += OnFireBombCanceled;

            var aimLock = _gameplayMap.FindAction("AimLock");
            if (aimLock != null)
            {
                aimLock.started += OnAimLockStarted;
                aimLock.canceled += OnAimLockCanceled;
            }
        }

        private void UnbindActions()
        {
            if (_gameplayMap == null) return;

            _gameplayMap["Move"].performed -= OnMove;
            _gameplayMap["Move"].canceled -= OnMoveCanceled;
            _gameplayMap["BreakBombHold"].started -= OnBreakBombStarted;
            _gameplayMap["BreakBombHold"].canceled -= OnBreakBombCanceled;
            _gameplayMap["FireBombHold"].started -= OnFireBombStarted;
            _gameplayMap["FireBombHold"].canceled -= OnFireBombCanceled;

            var aimLock = _gameplayMap.FindAction("AimLock");
            if (aimLock != null)
            {
                aimLock.started -= OnAimLockStarted;
                aimLock.canceled -= OnAimLockCanceled;
            }
        }

        private void OnMove(InputAction.CallbackContext ctx)
        {
            var vec = ctx.ReadValue<Vector2>();
            var dir = Vector2ToDirection8(vec);
            if (dir.HasValue)
            {
                // ダブルタップ検出
                float now = _timeProvider.UnscaledTime;
                if (_lastTapDirection.HasValue
                    && _lastTapDirection.Value == dir.Value
                    && now - _lastTapTime <= _doubleTapWindow)
                {
                    OnDashTriggered?.Invoke(_playerId, dir.Value);
                    _lastTapDirection = null; // リセットして連続発動防止
                }
                else
                {
                    _lastTapDirection = dir.Value;
                    _lastTapTime = now;
                }

                _lastDirection = dir.Value;
                _heldDirection = dir.Value;
                OnMoveInput?.Invoke(_playerId, dir.Value);
            }
        }

        private void OnMoveCanceled(InputAction.CallbackContext ctx)
        {
            _heldDirection = null;
            OnMoveReleased?.Invoke(_playerId);
        }

        private void OnBreakBombStarted(InputAction.CallbackContext ctx)
            => OnBombHoldInput?.Invoke(new BombHoldCommand(_playerId, BombType.Break, true));

        private void OnBreakBombCanceled(InputAction.CallbackContext ctx)
            => OnBombHoldInput?.Invoke(new BombHoldCommand(_playerId, BombType.Break, false));

        private void OnFireBombStarted(InputAction.CallbackContext ctx)
            => OnBombHoldInput?.Invoke(new BombHoldCommand(_playerId, BombType.Fire, true));

        private void OnFireBombCanceled(InputAction.CallbackContext ctx)
            => OnBombHoldInput?.Invoke(new BombHoldCommand(_playerId, BombType.Fire, false));

        private void OnAimLockStarted(InputAction.CallbackContext ctx) => _isAimLocked = true;
        private void OnAimLockCanceled(InputAction.CallbackContext ctx) => _isAimLocked = false;

        private static Direction8? Vector2ToDirection8(Vector2 v)
        {
            if (v.sqrMagnitude < 0.1f) return null;

            float angle = Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg;
            if (angle < 0) angle += 360f;

            int sector = Mathf.RoundToInt(angle / 45f) % 8;
            return sector switch
            {
                0 => Direction8.E,
                1 => Direction8.NE,
                2 => Direction8.N,
                3 => Direction8.NW,
                4 => Direction8.W,
                5 => Direction8.SW,
                6 => Direction8.S,
                7 => Direction8.SE,
                _ => null,
            };
        }
    }
}
