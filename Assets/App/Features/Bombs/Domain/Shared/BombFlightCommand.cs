using FloorBreaker.Shared.Domain.Grid;
using FloorBreaker.Shared.Domain.Primitives;

namespace FloorBreaker.Bombs.Domain
{
    public readonly struct BombFlightCommand
    {
        public readonly GridPos Origin;
        public readonly Direction8 Direction;
        public readonly BombSpec Spec;
        public readonly PlayerId Owner;

        public BombFlightCommand(GridPos origin, Direction8 direction, BombSpec spec, PlayerId owner)
        {
            Origin = origin;
            Direction = direction;
            Spec = spec;
            Owner = owner;
        }
    }
}
