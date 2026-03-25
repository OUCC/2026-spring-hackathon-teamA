using FloorBreaker.Shared.Domain.Primitives;

namespace FloorBreaker.Bombs.Domain
{
    public readonly struct BombSpec
    {
        public readonly BombType Type;
        public readonly int MaxFlightDistance;
        public readonly int EffectRange;
        public readonly int Damage;
        public readonly float Cooldown;
        public readonly bool HasFlightDamage;
        public readonly bool WallPenetration;
        public readonly float Duration;      // 炎ボム: 炎の持続時間
        public readonly float CollapseTime;  // 滑落ボム: 崩落時間
        public readonly float RecoveryTime;  // 滑落ボム: 復帰時間

        public BombSpec(
            BombType type,
            int maxFlightDistance,
            int effectRange,
            int damage,
            float cooldown,
            bool hasFlightDamage,
            bool wallPenetration,
            float duration,
            float collapseTime,
            float recoveryTime)
        {
            Type = type;
            MaxFlightDistance = maxFlightDistance;
            EffectRange = effectRange;
            Damage = damage;
            Cooldown = cooldown;
            HasFlightDamage = hasFlightDamage;
            WallPenetration = wallPenetration;
            Duration = duration;
            CollapseTime = collapseTime;
            RecoveryTime = recoveryTime;
        }
    }
}
