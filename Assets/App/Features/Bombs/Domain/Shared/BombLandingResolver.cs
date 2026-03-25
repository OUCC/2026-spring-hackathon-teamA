using System;
using FloorBreaker.Shared.Domain.Grid;
using FloorBreaker.Stage.Domain;

namespace FloorBreaker.Bombs.Domain
{
    public sealed class BombLandingResolver
    {
        private readonly StageModel _stage;

        public BombLandingResolver(StageModel stage)
        {
            _stage = stage;
        }

        /// <summary>
        /// ボムの着弾位置を解決する。
        /// actualFlightDistance はボタンリリース時の飛行距離（spec.MaxFlightDistance 以下）。
        /// isEntityAt はプレイヤー・スライムの位置判定（null 可）。
        /// </summary>
        public GridPos Resolve(BombFlightCommand cmd, int actualFlightDistance, Func<GridPos, bool> isEntityAt)
        {
            var offset = cmd.Direction.ToOffset();
            var lastValid = cmd.Origin;

            for (int i = 1; i <= actualFlightDistance; i++)
            {
                var pos = cmd.Origin + offset * i;

                if (!_stage.IsInBounds(pos))
                    return lastValid;

                var state = _stage.GetTileState(pos);

                // 通行不可タイル（Collapsed, PermanentlyDestroyed）: 手前で着弾
                if (state == TileState.Collapsed || state == TileState.PermanentlyDestroyed)
                    return lastValid;

                // 壁衝突: 壁の位置で着弾（効果範囲で壁が破壊される）
                if (state == TileState.Wall)
                    return pos;

                // エンティティ衝突: そのマスで着弾
                if (isEntityAt != null && isEntityAt(pos))
                    return pos;

                lastValid = pos;
            }

            return lastValid;
        }
    }
}
