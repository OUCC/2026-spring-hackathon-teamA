using FloorBreaker.Shared.Domain.Primitives;

namespace FloorBreaker.Bombs.Domain
{
    public readonly struct BombSpec
    {
        public readonly BombType Type;
        public readonly int MaxFlightDistance;
        public readonly int MinFlightDistance;
        public readonly int EffectRange;
        public readonly int Damage;
        public readonly float Cooldown;
        public readonly bool WallPenetration;
        public readonly bool FlightPenetration; // 飛行中の障害物貫通（壁・エンティティを無視）
        public readonly float Duration;      // 炎ボム: 炎の持続時間
        public readonly float CollapseTime;  // ブレークボム: 崩落時間
        public readonly float RecoveryTime;  // ブレークボム: 復帰時間

        public BombSpec(
            BombType type,
            int maxFlightDistance,
            int minFlightDistance,
            int effectRange,
            int damage,
            float cooldown,
            bool wallPenetration,
            float duration,
            float collapseTime,
            float recoveryTime,
            bool flightPenetration = false)
        {
            Type = type;
            MaxFlightDistance = maxFlightDistance;
            MinFlightDistance = minFlightDistance;
            EffectRange = effectRange;
            Damage = damage;
            Cooldown = cooldown;
            WallPenetration = wallPenetration;
            FlightPenetration = flightPenetration;
            Duration = duration;
            CollapseTime = collapseTime;
            RecoveryTime = recoveryTime;
        }
    }
}
