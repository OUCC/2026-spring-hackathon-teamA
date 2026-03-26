using System;
using UnityEngine;
using UnityEngine.InputSystem;
using FloorBreaker.Shared.Domain.Grid;
using FloorBreaker.Shared.Domain.Primitives;
using FloorBreaker.Bombs.Domain;

namespace FloorBreaker.Input.Infrastructure
{
    /// <summary>
    /// Input System の PlayerInput コンポーネントから入力を読み取り、
    /// Domain 型に変換して Application 層に渡す。
    /// プレイヤーごとに1つ配置する。
    /// </summary>
    public sealed class PlayerInputAdapter : MonoBehaviour
    {
        [SerializeField] private PlayerInput playerInput;

        private PlayerId _playerId;
        private Direction8 _lastDirection = Direction8.S;
        private Direction8? _heldDirection;
        private bool _isAimLocked;

        public PlayerId Owner => _playerId;
        public Direction8 LastDirection => _lastDirection;

        /// <summary>現在ホールド中の方向。null = スティック/十字キーがニュートラル。</summary>
        public Direction8? HeldDirection => _heldDirection;

        /// <summary>AimLock ボタンが押されているか。true の間は移動せず向きだけ変える。</summary>
        public bool IsAimLocked => _isAimLocked;

        public event Action<PlayerId, Direction8> OnMoveInput;
        public event Action<PlayerId> OnMoveReleased;
        public event Action<BombHoldCommand> OnBombHoldInput;

        public void Initialize(PlayerId playerId)
        {
            _playerId = playerId;
        }

        private void OnEnable()
        {
            if (playerInput == null) return;

            var gameplay = playerInput.actions.FindActionMap("Gameplay");
            if (gameplay == null) return;

            gameplay["Move"].performed += OnMove;
            gameplay["Move"].canceled += OnMoveCanceled;
            gameplay["FallBombHold"].started += OnFallBombStarted;
            gameplay["FallBombHold"].canceled += OnFallBombCanceled;
            gameplay["FireBombHold"].started += OnFireBombStarted;
            gameplay["FireBombHold"].canceled += OnFireBombCanceled;

            var aimLock = gameplay.FindAction("AimLock");
            if (aimLock != null)
            {
                aimLock.started += OnAimLockStarted;
                aimLock.canceled += OnAimLockCanceled;
            }
        }

        private void OnDisable()
        {
            if (playerInput == null) return;

            var gameplay = playerInput.actions.FindActionMap("Gameplay");
            if (gameplay == null) return;

            gameplay["Move"].performed -= OnMove;
            gameplay["Move"].canceled -= OnMoveCanceled;
            gameplay["FallBombHold"].started -= OnFallBombStarted;
            gameplay["FallBombHold"].canceled -= OnFallBombCanceled;
            gameplay["FireBombHold"].started -= OnFireBombStarted;
            gameplay["FireBombHold"].canceled -= OnFireBombCanceled;

            var aimLock = gameplay.FindAction("AimLock");
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
        {
            OnBombHoldInput?.Invoke(new BombHoldCommand(_playerId, BombType.Fall, true));
        }

        private void OnFallBombCanceled(InputAction.CallbackContext ctx)
        {
            OnBombHoldInput?.Invoke(new BombHoldCommand(_playerId, BombType.Fall, false));
        }

        private void OnFireBombStarted(InputAction.CallbackContext ctx)
        {
            OnBombHoldInput?.Invoke(new BombHoldCommand(_playerId, BombType.Fire, true));
        }

        private void OnFireBombCanceled(InputAction.CallbackContext ctx)
        {
            OnBombHoldInput?.Invoke(new BombHoldCommand(_playerId, BombType.Fire, false));
        }

        private void OnAimLockStarted(InputAction.CallbackContext ctx)
        {
            _isAimLocked = true;
        }

        private void OnAimLockCanceled(InputAction.CallbackContext ctx)
        {
            _isAimLocked = false;
        }

        private static Direction8? Vector2ToDirection8(Vector2 v)
        {
            if (v.sqrMagnitude < 0.1f) return null;

            float angle = Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg;
            if (angle < 0) angle += 360f;

            // 8方向に量子化（各方向は45度の扇形）
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
