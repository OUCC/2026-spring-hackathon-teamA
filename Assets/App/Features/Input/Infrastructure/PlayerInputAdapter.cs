using System;
using UnityEngine;
using UnityEngine.InputSystem;
using FloorBreaker.Shared.Domain.Grid;
using FloorBreaker.Shared.Domain.Primitives;
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

        public void Initialize(PlayerId playerId, InputActionAsset actions = null)
        {
            _playerId = playerId;
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

            // P1 → Gameplay_P1, P2 → Gameplay_P2
            string mapName = _playerId == PlayerId.Player1 ? "Gameplay_P1" : "Gameplay_P2";
            _gameplayMap = _inputActions.FindActionMap(mapName);
            if (_gameplayMap == null) return;

            _gameplayMap.Enable();

            _gameplayMap["Move"].performed += OnMove;
            _gameplayMap["Move"].canceled += OnMoveCanceled;
            _gameplayMap["FallBombHold"].started += OnFallBombStarted;
            _gameplayMap["FallBombHold"].canceled += OnFallBombCanceled;
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
            _gameplayMap["FallBombHold"].started -= OnFallBombStarted;
            _gameplayMap["FallBombHold"].canceled -= OnFallBombCanceled;
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

        private void OnFallBombStarted(InputAction.CallbackContext ctx)
            => OnBombHoldInput?.Invoke(new BombHoldCommand(_playerId, BombType.Fall, true));

        private void OnFallBombCanceled(InputAction.CallbackContext ctx)
            => OnBombHoldInput?.Invoke(new BombHoldCommand(_playerId, BombType.Fall, false));

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
