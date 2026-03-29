using System;
using System.Collections.Generic;
using Fusion;
using R3;
using FloorBreaker.Shared.Domain.Grid;
using FloorBreaker.Slimes.Domain;

namespace FloorBreaker.Network.Infrastructure
{
    /// <summary>
    /// スライム状態の同期。
    /// ホスト: SlimeRegistry のイベント → RPC
    /// クライアント: RPC → SlimeRegistry に反映
    /// </summary>
    public class NetworkSlimeStateAdapter : NetworkBehaviour
    {
        private SlimeRegistry _registry;
        private IDisposable _spawnedSub;
        private IDisposable _movedSub;
        private IDisposable _killedSub;

        // 移動バッチ
        private readonly List<int> _moveId = new();
        private readonly List<int> _movePosX = new();
        private readonly List<int> _movePosY = new();

        public void Initialize(SlimeRegistry registry)
        {
            _registry = registry;

            if (Object != null && Object.HasStateAuthority && _registry != null)
            {
                _spawnedSub = _registry.Spawned.Subscribe(e =>
                    RPC_SlimeSpawned(e.Id.Value, (byte)e.Type, e.Position.X, e.Position.Y));

                _movedSub = _registry.Moved.Subscribe(e =>
                {
                    _moveId.Add(e.Id.Value);
                    _movePosX.Add(e.NewPosition.X);
                    _movePosY.Add(e.NewPosition.Y);
                });

                _killedSub = _registry.Killed.Subscribe(e =>
                    RPC_SlimeKilled(e.Id.Value, (byte)e.Type, e.Position.X, e.Position.Y));
            }
        }

        public override void Spawned()
        {
            // 購読は Initialize() で行う
        }

        /// <summary>ホスト側: Tick 末尾で呼ぶ。移動バッチを RPC 送信。</summary>
        public void FlushMoveBatch()
        {
            if (_moveId.Count > 0)
            {
                RPC_SlimeMoveBatch(_moveId.ToArray(), _movePosX.ToArray(), _movePosY.ToArray());
                _moveId.Clear();
                _movePosX.Clear();
                _movePosY.Clear();
            }
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_SlimeSpawned(int id, byte type, int posX, int posY)
        {
            if (Object.HasStateAuthority || _registry == null) return;
            // クライアント側でスライムをレジストリに追加
            // → Presentation の SlimePresenter が Spawned イベントを購読して生成する
            _registry.AddRemote(new SlimeId(id), (SlimeType)type, new GridPos(posX, posY));
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_SlimeMoveBatch(int[] ids, int[] posX, int[] posY)
        {
            if (Object.HasStateAuthority || _registry == null) return;
            for (int i = 0; i < ids.Length; i++)
            {
                var slime = _registry.TryGet(new SlimeId(ids[i]));
                slime?.MoveTo(new GridPos(posX[i], posY[i]));
            }
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_SlimeKilled(int id, byte type, int posX, int posY)
        {
            if (Object.HasStateAuthority || _registry == null) return;
            var slime = _registry.TryGet(new SlimeId(id));
            if (slime != null && slime.IsAlive)
                slime.Kill();
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            _spawnedSub?.Dispose();
            _movedSub?.Dispose();
            _killedSub?.Dispose();
        }
    }
}
