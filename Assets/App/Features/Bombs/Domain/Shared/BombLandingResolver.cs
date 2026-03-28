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
            bool penetrating = cmd.Spec.FlightPenetration;

            for (int i = 1; i <= actualFlightDistance; i++)
            {
                var pos = cmd.Origin + offset * i;

                if (!_stage.IsInBounds(pos))
                    return lastValid;

                var data = _stage.GetTileData(pos);

                // 穴（Collapsed, PermanentlyDestroyed）: ボムは飛び越える（lastValid 更新なし）
                if (TileData.IsHoleCondition(data.Condition))
                    continue;

                // 壁衝突 (Wall, Bedrock)
                if (TileData.IsImpassableType(data.Type))
                {
                    if (penetrating)
                    {
                        lastValid = pos;
                        continue; // 貫通: 壁を無視して飛行続行
                    }
                    return pos; // 通常: 壁の位置で着弾
                }

                // エンティティ衝突
                if (isEntityAt != null && isEntityAt(pos))
                {
                    if (penetrating)
                    {
                        lastValid = pos;
                        continue; // 貫通: エンティティを無視して飛行続行
                    }
                    return pos; // 通常: そのマスで着弾
                }

                lastValid = pos;
            }

            return lastValid;
        }
    }
}
