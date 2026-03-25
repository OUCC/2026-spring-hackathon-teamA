namespace FloorBreaker.Shared.Domain.Primitives
{
    public enum UpgradeId : byte
    {
        None = 0,

        // 炎ボム強化
        FireFlightRange,
        FireEffectRange,
        FireDamage,
        FireFlightDamage,
        FireDuration,
        FireWallPenetration,
        FireCooldown,

        // 滑落ボム強化
        FallFlightRange,
        FallEffectRange,
        FallDamage,
        FallFlightDamage,
        FallCollapseTime,
        FallCooldown,

        // 汎用強化
        MoveSpeed,
        HpRecovery,
    }
}
