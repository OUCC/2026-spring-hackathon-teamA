using FloorBreaker.Shared.Domain.Primitives;

namespace FloorBreaker.Bombs.Domain
{
    public readonly struct BombHoldCommand
    {
        public readonly PlayerId Owner;
        public readonly BombType Type;
        public readonly bool IsPressed;

        public BombHoldCommand(PlayerId owner, BombType type, bool isPressed)
        {
            Owner = owner;
            Type = type;
            IsPressed = isPressed;
        }
    }
}
