using System;
using FloorBreaker.Shared.Domain.Grid;

namespace FloorBreaker.Slimes.Domain
{
    public sealed class SlimeModel
    {
        public SlimeId Id { get; }
        public SlimeType Type { get; }
        public GridPos Position { get; private set; }
        public bool IsAlive { get; private set; }
        public float AttackCooldownRemaining { get; private set; }
        public float MoveAccumulator { get; set; }

        public SlimeModel(SlimeId id, SlimeType type, GridPos position, float initialAttackCooldown = 1f)
        {
            Id = id;
            Type = type;
            Position = position;
            IsAlive = true;
            AttackCooldownRemaining = initialAttackCooldown;
        }

        public void Kill()
        {
            IsAlive = false;
        }

        public void MoveTo(GridPos target)
        {
            Position = target;
        }

        public void ResetAttackCooldown(float cooldown)
        {
            AttackCooldownRemaining = cooldown;
        }

        public bool CanAttack => AttackCooldownRemaining <= 0f;

        public void Tick(float deltaTime)
        {
            if (AttackCooldownRemaining > 0f)
                AttackCooldownRemaining = MathF.Max(0f, AttackCooldownRemaining - deltaTime);
        }
    }
}
