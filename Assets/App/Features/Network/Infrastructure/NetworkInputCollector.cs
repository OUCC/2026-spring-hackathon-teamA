using System;
using Fusion;
using FloorBreaker.Shared.Domain.Primitives;
using FloorBreaker.Shared.Application.Interfaces;
using FloorBreaker.Player.Domain;
using FloorBreaker.Bombs.Domain;
using FloorBreaker.Input.Infrastructure;

namespace FloorBreaker.Network.Infrastructure
{
    /// <summary>
    /// クライアント側の入力収集器。
    /// PlayerInputAdapter のイベントを ClientInputState に中継し、
    /// Fusion の OnInput() コールバックで FloorBreakerInput を生成する。
    /// </summary>
    public sealed class NetworkInputCollector : IDisposable
    {
        private readonly ClientInputState _state;
        private readonly PlayerModel _localPlayer;
        private PlayerInputAdapter _adapter;

        public NetworkInputCollector(IBalanceParameters balance, PlayerModel localPlayer)
        {
            _localPlayer = localPlayer;
            _state = new ClientInputState(
                balance.InputBufferTime,
                balance.InputInitialRepeatDelay,
                balance.InputBaseMoveInterval);
        }

        /// <summary>
        /// ローカルプレイヤーの PlayerInputAdapter を接続する。
        /// イベントを ClientInputState に転送する。
        /// </summary>
        public void BindAdapter(PlayerInputAdapter adapter)
        {
            UnbindAdapter();
            _adapter = adapter;
            _adapter.OnMoveInput += HandleMoveInput;
            _adapter.OnMoveReleased += HandleMoveReleased;
            _adapter.OnBombHoldInput += HandleBombHold;
            _adapter.OnDashTriggered += HandleDash;
        }

        /// <summary>
        /// FusionCallbacksBridge.OnInput() から呼ばれる。
        /// ClientInputState をポーリ��グし FloorBreakerInput を返す。
        /// </summary>
        public FloorBreakerInput CollectInput(NetworkRunner runner)
        {
            // アダ��ターの現在ホールド方向を反映
            if (_adapter != null && _adapter.HeldDirection.HasValue)
            {
                _state.UpdateHeldDirection(_adapter.HeldDirection.Value);
            }

            return _state.Poll(runner.DeltaTime, _localPlayer.Stats.MoveSpeed);
        }

        /// <summary>強化フェーズ中の UI アクションをセットする。</summary>
        public void SetUpgradeAction(UpgradeInputAction action)
        {
            _state.SetUpgradeAction(action);
        }

        private void HandleMoveInput(PlayerId playerId, Shared.Domain.Grid.Direction8 direction)
        {
            _state.OnMovePressed(direction);
        }

        private void HandleMoveReleased(PlayerId playerId)
        {
            _state.OnMoveReleased();
        }

        private void HandleBombHold(BombHoldCommand cmd)
        {
            _state.OnBombHold(cmd.Type, cmd.IsPressed);
        }

        private void HandleDash(PlayerId playerId, Shared.Domain.Grid.Direction8 direction)
        {
            _state.OnDashTriggered(direction);
        }

        private void UnbindAdapter()
        {
            if (_adapter == null) return;
            _adapter.OnMoveInput -= HandleMoveInput;
            _adapter.OnMoveReleased -= HandleMoveReleased;
            _adapter.OnBombHoldInput -= HandleBombHold;
            _adapter.OnDashTriggered -= HandleDash;
            _adapter = null;
        }

        public void Dispose()
        {
            UnbindAdapter();
        }
    }
}
