namespace FloorBreaker.Shared.Domain.Primitives
{
    public enum UpgradeId : byte
    {
        None = 0,

        // 炎ボム強化
        FireFlightRange,
        FireEffectRange,
        FireDamage,
        FireDuration,
        FireWallPenetration,
        FireCooldown,
        FireBombPenetration,

        // ブレークボム強化
        BreakFlightRange,
        BreakEffectRange,
        BreakDamage,
        BreakCollapseTime,
        BreakCooldown,
        BreakBombPenetration,

        // 汎用強化
        MoveSpeed,
        HpRecovery,

        // 汎用強化 — 一時効果
        FireShield,
        Levitation,

        // 汎用強化 — 永続アビリティ
        Dash,
        DualShot,
    }
}
