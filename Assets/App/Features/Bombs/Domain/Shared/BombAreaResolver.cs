using System.Collections.Generic;
using FloorBreaker.Shared.Domain.Grid;
using FloorBreaker.Stage.Domain;

namespace FloorBreaker.Bombs.Domain
{
    public sealed class BombAreaResolver
    {
        private readonly StageQueryService _stageQuery;

        public BombAreaResolver(StageQueryService stageQuery)
        {
            _stageQuery = stageQuery;
        }

        public IReadOnlyList<GridPos> Resolve(GridPos center, int range, bool penetrateWalls)
        {
            return _stageQuery.GetTilesInCross(center, range, penetrateWalls);
        }
    }
}
