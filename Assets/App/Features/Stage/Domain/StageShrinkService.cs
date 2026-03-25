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
                model.SetTileState(pos, TileState.PermanentlyDestroyed);
            }

            model.Bounds.Shrink();

            return ring;
        }
    }
}
