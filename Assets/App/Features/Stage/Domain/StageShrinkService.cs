using System.Collections.Generic;
using FloorBreaker.Shared.Domain.Grid;

namespace FloorBreaker.Stage.Domain
{
    public sealed class StageShrinkService
    {
        public IReadOnlyList<GridPos> ShrinkOuterRing(StageModel model)
        {
            var ring = model.Bounds.GetOuterRing();

            foreach (var pos in ring)
            {
                if (model.GetTileType(pos) == TileType.Bedrock) continue;
                model.SetTileCondition(pos, TileCondition.PermanentlyDestroyed);
            }

            model.Bounds.Shrink();

            return ring;
        }
    }
}
