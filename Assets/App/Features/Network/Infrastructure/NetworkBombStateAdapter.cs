using System;
using Fusion;
using R3;
using FloorBreaker.Shared.Domain.Grid;
using FloorBreaker.Shared.Domain.Primitives;
using FloorBreaker.Bombs.Application;

namespace FloorBreaker.Network.Infrastructure
{
    /// <summary>
    /// ボム発射・着弾イベントの同期。
    /// ホスト: BombFlightTracker のイベント購読 → RPC 送信
    /// クライアント: RPC 受信 → Presentation イベント発火用 Observable
    /// 飛行中のフレーム単位位置は同期しない（クライアントがローカル補間）。
    /// </summary>
    public class NetworkBombStateAdapter : NetworkBehaviour
    {
        private BombFlightTracker _tracker;
        private IDisposable _flightStartedSub;
        private IDisposable _bombLandedSub;

        // クライアント側イベント（Presentation が購読）
        public event Action<BombFlightStartedEvent> OnRemoteFlightStarted;
        public event Action<BombLandedEvent> OnRemoteBombLanded;

        public void Initialize(BombFlightTracker tracker)
        {
            _tracker = tracker;

            if (Object != null && Object.HasStateAuthority && _tracker != null)
            {
                _flightStartedSub = _tracker.FlightStarted.Subscribe(e =>
                    RPC_BombFlightStarted(e.Owner.Index, e.Origin.X, e.Origin.Y, (byte)e.Direction,
                        (byte)e.Spec.Type, e.Spec.MaxFlightDistance, e.Spec.EffectRange));

                _bombLandedSub = _tracker.BombLanded.Subscribe(e =>
                    RPC_BombLanded(e.Owner.Index, e.LandingPos.X, e.LandingPos.Y,
                        (byte)e.Type, e.EffectRange));
            }
        }

        public override void Spawned()
        {
            // 購読は Initialize() で行う（Spawned 時点では _tracker が null）
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_BombFlightStarted(int ownerIdx, int originX, int originY, byte direction,
            byte bombType, int flightDist, int effectRange)
        {
            if (Object.HasStateAuthority) return;

            var e = new BombFlightStartedEvent(
                PlayerId.FromIndex(ownerIdx),
                new GridPos(originX, originY),
                (Direction8)direction,
                default); // Spec は Presentation に最低限の情報だけ渡す

            OnRemoteFlightStarted?.Invoke(e);
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_BombLanded(int ownerIdx, int landingX, int landingY, byte bombType, int effectRange)
        {
            if (Object.HasStateAuthority) return;

            var e = new BombLandedEvent(
                PlayerId.FromIndex(ownerIdx),
                new GridPos(landingX, landingY),
                (BombType)bombType,
                effectRange,
                false);

            OnRemoteBombLanded?.Invoke(e);
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            _flightStartedSub?.Dispose();
            _bombLandedSub?.Dispose();
        }
    }
}
