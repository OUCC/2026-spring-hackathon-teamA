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
    /// ホスト: TileChanged イベント → バッチ RPC + 定期フルスナップショット（分割送信）
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

        // RPC ペイロード上限対策: 1回の RPC で送る最大タイル数
        // 4byte/tile * 100 = 400byte（ヘッダ含めて 512byte 以内）
        private const int MaxTilesPerRpc = 100;

        public void Initialize(StageModel stage)
        {
            _stage = stage;
        }

        public override void Spawned()
        {
            if (Object.HasStateAuthority && _stage != null)
            {
                _tileChangedSub = _stage.TileChanged.Subscribe(e => OnTileChanged(e));
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
                // バッチも分割送信（大量のタイル変更時）
                SendBatchInChunks();
                _batchPosX.Clear();
                _batchPosY.Clear();
                _batchType.Clear();
                _batchCondition.Clear();
                _batchWarpPairId.Clear();
            }

            _ticksSinceSnapshot++;
            if (_ticksSinceSnapshot >= SnapshotIntervalTicks)
            {
                _ticksSinceSnapshot = 0;
                SendFullSnapshot();
            }
        }

        private void SendBatchInChunks()
        {
            int total = _batchPosX.Count;
            for (int offset = 0; offset < total; offset += MaxTilesPerRpc)
            {
                int count = Math.Min(MaxTilesPerRpc, total - offset);
                var px = new int[count];
                var py = new int[count];
                var bt = new byte[count];
                var bc = new byte[count];
                var bw = new short[count];
                for (int i = 0; i < count; i++)
                {
                    px[i] = _batchPosX[offset + i];
                    py[i] = _batchPosY[offset + i];
                    bt[i] = _batchType[offset + i];
                    bc[i] = _batchCondition[offset + i];
                    bw[i] = _batchWarpPairId[offset + i];
                }
                RPC_TileBatchChanged(px, py, bt, bc, bw);
            }
        }

        private void SendFullSnapshot()
        {
            if (_stage == null) return;
            var tiles = _stage.GetTilesRaw();
            int w = tiles.GetLength(0);
            int h = tiles.GetLength(1);
            int totalTiles = w * h;

            // 分割送信: MaxTilesPerRpc タイルずつ
            for (int offset = 0; offset < totalTiles; offset += MaxTilesPerRpc)
            {
                int count = Math.Min(MaxTilesPerRpc, totalTiles - offset);
                var data = new byte[count * 4];
                int idx = 0;

                for (int i = 0; i < count; i++)
                {
                    int tileIdx = offset + i;
                    int x = tileIdx % w;
                    int y = tileIdx / w;
                    var tile = tiles[x, y];
                    data[idx++] = (byte)tile.Type;
                    data[idx++] = (byte)tile.Condition;
                    data[idx++] = (byte)(tile.WarpPairId & 0xFF);
                    data[idx++] = (byte)((tile.WarpPairId >> 8) & 0xFF);
                }

                RPC_SnapshotChunk(w, h, offset, count, data);
            }
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_TileBatchChanged(int[] posX, int[] posY, byte[] type, byte[] condition, short[] warpPairId)
        {
            if (Object.HasStateAuthority || _stage == null) return;

            for (int i = 0; i < posX.Length; i++)
            {
                var pos = new GridPos(posX[i], posY[i]);
                var tileData = new TileData
                {
                    Type = (TileType)type[i],
                    Condition = (TileCondition)condition[i],
                    WarpPairId = warpPairId[i],
                };
                _stage.SetTileData(pos, tileData);
            }
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_SnapshotChunk(int width, int height, int tileOffset, int tileCount, byte[] data)
        {
            if (Object.HasStateAuthority || _stage == null) return;

            int idx = 0;
            for (int i = 0; i < tileCount; i++)
            {
                int tileIdx = tileOffset + i;
                int x = tileIdx % width;
                int y = tileIdx / width;
                var pos = new GridPos(x, y);

                var tileData = new TileData
                {
                    Type = (TileType)data[idx],
                    Condition = (TileCondition)data[idx + 1],
                    WarpPairId = (short)(data[idx + 2] | (data[idx + 3] << 8)),
                };
                idx += 4;

                _stage.SetTileData(pos, tileData);
            }
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            _tileChangedSub?.Dispose();
        }
    }
}
