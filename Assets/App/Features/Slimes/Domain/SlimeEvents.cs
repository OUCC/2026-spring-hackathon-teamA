using FloorBreaker.Shared.Domain.Grid;

namespace FloorBreaker.Slimes.Domain
{
    public readonly struct SlimeSpawnedEvent
    {
        public readonly SlimeId Id;
        public readonly SlimeType Type;
        public readonly GridPos Position;

        public SlimeSpawnedEvent(SlimeId id, SlimeType type, GridPos position)
        {
            Id = id;
            Type = type;
            Position = position;
        }
    }

    public readonly struct SlimeMovedEvent
    {
        public readonly SlimeId Id;
        public readonly GridPos OldPosition;
        public readonly GridPos NewPosition;

        public SlimeMovedEvent(SlimeId id, GridPos oldPosition, GridPos newPosition)
        {
            Id = id;
            OldPosition = oldPosition;
            NewPosition = newPosition;
        }
    }

    public readonly struct SlimeKilledEvent
    {
        public readonly SlimeId Id;
        public readonly SlimeType Type;
        public readonly GridPos Position;

        public SlimeKilledEvent(SlimeId id, SlimeType type, GridPos position)
        {
            Id = id;
            Type = type;
            Position = position;
        }
    }

    public readonly struct SlimeAttackedEvent
    {
        public readonly SlimeId AttackerId;
        public readonly GridPos AttackerPosition;
        public readonly GridPos TargetPosition;

        public SlimeAttackedEvent(SlimeId attackerId, GridPos attackerPosition, GridPos targetPosition)
        {
            AttackerId = attackerId;
            AttackerPosition = attackerPosition;
            TargetPosition = targetPosition;
        }
    }
}
