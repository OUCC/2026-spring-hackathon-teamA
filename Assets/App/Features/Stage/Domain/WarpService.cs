using System.Collections.Generic;
using FloorBreaker.Shared.Domain.Grid;

namespace FloorBreaker.Stage.Domain
{
    /// <summary>
    /// ワープマスのテレポートロジックを管理する。
    /// ペアの検索は辞書で O(1)。
    /// </summary>
    public sealed class WarpService
    {
        private readonly StageModel _stage;
        private readonly Dictionary<short, (GridPos a, GridPos b)> _pairs = new();

        public WarpService(StageModel stage)
        {
            _stage = stage;
        }

        /// <summary>
        /// ステージ初期化後に呼び出し、全ワープペアを登録する。
        /// </summary>
        public void BuildRegistry(TileCoordRange bounds)
        {
            _pairs.Clear();

            foreach (var pos in bounds.GetAllPositions())
            {
                var data = _stage.GetTileData(pos);
                if (data.Type != TileType.Warp || data.WarpPairId < 0) continue;

                if (_pairs.TryGetValue(data.WarpPairId, out var existing))
                {
                    _pairs[data.WarpPairId] = (existing.a, pos);
                }
                else
                {
                    _pairs[data.WarpPairId] = (pos, default);
                }
            }
        }

        /// <summary>
        /// 指定位置がワープマスならペア先を返す。ワープ条件を満たさない場合は null。
        /// </summary>
        public GridPos? TryGetWarpDestination(GridPos from)
        {
            var data = _stage.GetTileData(from);
            if (data.Type != TileType.Warp || data.Condition != TileCondition.Intact)
                return null;
            if (data.WarpPairId < 0)
                return null;

            if (!_pairs.TryGetValue(data.WarpPairId, out var pair))
                return null;

            var dest = pair.a.Equals(from) ? pair.b : pair.a;
            if (dest.Equals(default) && !pair.a.Equals(from))
                return null;

            var destData = _stage.GetTileData(dest);
            if (destData.Type != TileType.Warp) return null;
            if (TileData.IsHoleCondition(destData.Condition)) return null;

            return dest;
        }

        /// <summary>
        /// ペア片方が永久消滅した時に呼び出し、もう片方を Normal に変換する。
        /// StageShrinkService 等の呼び出し元から通知される。
        /// </summary>
        public void HandleTilePermanentlyDestroyed(GridPos pos)
        {
            var data = _stage.GetTileData(pos);
            if (data.Type != TileType.Warp || data.WarpPairId < 0) return;

            if (!_pairs.TryGetValue(data.WarpPairId, out var pair)) return;

            var other = pair.a.Equals(pos) ? pair.b : pair.a;
            if (other.Equals(default)) return;

            var otherData = _stage.GetTileData(other);
            if (otherData.Type == TileType.Warp)
            {
                _stage.SetTileData(other, new TileData
                {
                    Type = TileType.Normal,
                    Condition = otherData.Condition,
                    WarpPairId = -1,
                });
            }

            _pairs.Remove(data.WarpPairId);
        }
    }
}
