using System.Collections.Generic;
using FloorBreaker.Shared.Domain.Grid;

namespace FloorBreaker.Stage.Domain
{
    public readonly struct RaycastResult
    {
        public readonly GridPos HitPos;
        public readonly int Distance;
        public readonly TileState HitTileState;

        public RaycastResult(GridPos hitPos, int distance, TileState hitTileState)
        {
            HitPos = hitPos;
            Distance = distance;
            HitTileState = hitTileState;
        }
    }

    public sealed class StageQueryService
    {
        private readonly StageModel _model;

        public StageQueryService(StageModel model)
        {
            _model = model;
        }

        public IReadOnlyList<GridPos> GetPassableTiles()
        {
            var result = new List<GridPos>();
            foreach (var pos in _model.GetCurrentBounds().GetAllPositions())
            {
                if (_model.IsPassable(pos))
                    result.Add(pos);
            }
            return result;
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

                    var state = _model.GetTileState(pos);
                    if (!penetrateWalls && state == TileState.Wall)
                    {
                        result.Add(pos); // wall itself is included (will be destroyed)
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

                var state = _model.GetTileState(pos);
                if (!_model.IsPassable(pos))
                    return new RaycastResult(pos, i, state);
            }

            // Reached max distance without hitting anything
            var lastPos = new GridPos(from.X + offset.X * maxDist, from.Y + offset.Y * maxDist);
            if (_model.IsInBounds(lastPos))
                return new RaycastResult(lastPos, maxDist, _model.GetTileState(lastPos));

            return null;
        }
    }
}
