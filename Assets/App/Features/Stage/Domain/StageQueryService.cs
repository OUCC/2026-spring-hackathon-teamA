using System.Collections.Generic;
using FloorBreaker.Shared.Domain.Grid;

namespace FloorBreaker.Stage.Domain
{
    public readonly struct RaycastResult
    {
        public readonly GridPos HitPos;
        public readonly int Distance;
        public readonly TileData HitTileData;

        public RaycastResult(GridPos hitPos, int distance, TileData hitTileData)
        {
            HitPos = hitPos;
            Distance = distance;
            HitTileData = hitTileData;
        }
    }

    public sealed class StageQueryService
    {
        private readonly StageModel _model;

        public StageQueryService(StageModel model)
        {
            _model = model;
        }

        public IReadOnlyList<GridPos> GetTilesInCross(GridPos center, int range, bool penetrateWalls)
        {
            var result = new List<GridPos>();

            if (_model.IsInBounds(center))
                result.Add(center);

            var directions = new[]
            {
                CardinalDirection4.N,
                CardinalDirection4.E,
                CardinalDirection4.S,
                CardinalDirection4.W,
            };

            foreach (var dir in directions)
            {
                var offset = dir.ToOffset();
                for (int i = 1; i <= range; i++)
                {
                    var pos = new GridPos(center.X + offset.X * i, center.Y + offset.Y * i);
                    if (!_model.IsInBounds(pos)) break;

                    var tileType = _model.GetTileType(pos);
                    if (!penetrateWalls && !_model.IsPassable(pos))
                    {
                        // 壁は破壊対象として含める (Bedrock は含めない)、それ以外は含めない
                        if (tileType == TileType.Wall)
                            result.Add(pos);
                        break;
                    }

                    result.Add(pos);
                }
            }

            return result;
        }

        public RaycastResult? RaycastGrid(GridPos from, Direction8 dir, int maxDist)
        {
            var offset = dir.ToOffset();
            for (int i = 1; i <= maxDist; i++)
            {
                var pos = new GridPos(from.X + offset.X * i, from.Y + offset.Y * i);
                if (!_model.IsInBounds(pos))
                    return null;

                var data = _model.GetTileData(pos);
                if (!data.IsPassable)
                    return new RaycastResult(pos, i, data);
            }

            // Reached max distance without hitting anything
            var lastPos = new GridPos(from.X + offset.X * maxDist, from.Y + offset.Y * maxDist);
            if (_model.IsInBounds(lastPos))
                return new RaycastResult(lastPos, maxDist, _model.GetTileData(lastPos));

            return null;
        }
    }
}
