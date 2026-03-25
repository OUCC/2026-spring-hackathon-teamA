using System.Collections.Generic;
using FloorBreaker.Shared.Domain.Grid;

namespace FloorBreaker.Stage.Domain
{
    public sealed class StageBounds
    {
        public TileCoordRange Current { get; private set; }

        public StageBounds(TileCoordRange initial)
        {
            Current = initial;
        }

        public IReadOnlyList<GridPos> GetOuterRing() => Current.GetOuterRing();

        public void Shrink()
        {
            Current = Current.Shrink(1);
        }
    }
}
