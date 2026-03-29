using System;
using System.Collections.Generic;
using Fusion;
using R3;
using UnityEngine;
using FloorBreaker.Shared.Domain.Grid;
using FloorBreaker.Stage.Domain;

namespace FloorBreaker.Network.Infrastructure
{
    /// <summary>
    /// タイル状態の同期。
    /// ホスト: TileChanged イベント → バッチ RPC + 定期フルスナップショット
    /// クライアント: RPC → StageModel.SetTileData()
    /// </summary>
    public class NetworkStageStateAdapter : NetworkBehaviour
    {
        private StageModel _stage;
        private IDisposable _tileChangedSub;

        // バッチ用バッファ（ホスト側、同一 Tick 内の変更を蓄積）
        private readonly List<int> _batchPosX = new();
        private readonly List<int> _batchPosY = new();
        private readonly List<byte> _batchType = new();
        private readonly List<byte> _batchCondition = new();
        private readonly List<short> _batchWarpPairId = new();

        // フルスナップショット周期
        private int _ticksSinceSnapshot;
        private const int SnapshotIntervalTicks = 150; // 5秒 @30Hz

        public void Initialize(StageModel stage)
        {
            _stage = stage;
        }

        public override void Spawned()
        {
            if (Object.HasStateAuthority && _stage != null)
            {
                // ホスト: タイル変更を購読してバッファに蓄積
                _tileChangedSub = _stage.TileChanged.Subscribe(e => OnTileChanged(e));

                // 初回フルスナップショット送信
                SendFullSnapshot();
            }
        }

        private void OnTileChanged(TileChangedEvent e)
        {
            _batchPosX.Add(e.Pos.X);
            _batchPosY.Add(e.Pos.Y);
            _batchType.Add((byte)e.NewData.Type);
            _batchCondition.Add((byte)e.NewData.Condition);
            _batchWarpPairId.Add(e.NewData.WarpPairId);
        }

        /// <summary>ホスト側: Tick 末尾で呼ぶ。バッファを RPC 送信してクリア。</summary>
        public void FlushBatch()
        {
            if (_batchPosX.Count > 0)
            {
                RPC_TileBatchChanged(
                    _batchPosX.ToArray(), _batchPosY.ToArray(),
                    _batchType.ToArray(), _batchCondition.ToArray(),
                    _batchWarpPairId.ToArray());

                _batchPosX.Clear();
                _batchPosY.Clear();
                _batchType.Clear();
                _batchCondition.Clear();
                _batchWarpPairId.Clear();
            }

            // 定期フルスナップショット
            _ticksSinceSnapshot++;
            if (_ticksSinceSnapshot >= SnapshotIntervalTicks)
            {
                _ticksSinceSnapshot = 0;
                SendFullSnapshot();
            }
        }

        private void SendFullSnapshot()
        {
            if (_stage == null) return;
            var bounds = _stage.GetCurrentBounds();
            var tiles = _stage.GetTilesRaw();
            int w = tiles.GetLength(0);
            int h = tiles.GetLength(1);

            // バイト配列にパック: type(1) + condition(1) + warpPairId(2) = 4byte/tile
            var data = new byte[w * h * 4];
            int idx = 0;
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    var tile = tiles[x, y];
                    data[idx++] = (byte)tile.Type;
                    data[idx++] = (byte)tile.Condition;
                    data[idx++] = (byte)(tile.WarpPairId & 0xFF);
                    data[idx++] = (byte)((tile.WarpPairId >> 8) & 0xFF);
                }
            }

            RPC_FullSnapshot(w, h, data);
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_TileBatchChanged(int[] posX, int[] posY, byte[] type, byte[] condition, short[] warpPairId)
        {
            if (Object.HasStateAuthority || _stage == null) return;

            for (int i = 0; i < posX.Length; i++)
            {
                var pos = new GridPos(posX[i], posY[i]);
                var data = new TileData
                {
                    Type = (TileType)type[i],
                    Condition = (TileCondition)condition[i],
                    WarpPairId = warpPairId[i],
                };
                _stage.SetTileData(pos, data);
            }
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_FullSnapshot(int width, int height, byte[] data)
        {
            if (Object.HasStateAuthority || _stage == null) return;

            var snapshot = new TileData[width, height];
            int idx = 0;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    snapshot[x, y] = new TileData
                    {
                        Type = (TileType)data[idx],
                        Condition = (TileCondition)data[idx + 1],
                        WarpPairId = (short)(data[idx + 2] | (data[idx + 3] << 8)),
                    };
                    idx += 4;
                }
            }

            _stage.LoadSnapshot(snapshot);
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            _tileChangedSub?.Dispose();
        }
    }
}
