namespace FloorBreaker.Shared.Application.Interfaces
{
    /// <summary>
    /// 全ゲームバランスパラメータの読み取り専用インターフェース。
    /// 実装は ScriptableObject (BalanceConfig) が担う。
    /// </summary>
    public interface IBalanceParameters
    {
        // --- Player ---
        int InitialHp { get; }
        float BaseMovementSpeed { get; }
        float MaxMovementSpeed { get; }
        float MovementSpeedIncrement { get; }

        // --- Fall Bomb ---
        int FallBombMaxFlightDistance { get; }
        int FallBombEffectRange { get; }
        int FallBombDamage { get; }
        float FallBombCollapseDuration { get; }
        float FallBombRecoveryDuration { get; }
        float FallBombCooldown { get; }
        float FallBombCooldownMin { get; }
        float FallBombCooldownReduction { get; }
        bool FallBombDefaultWallPenetration { get; }

        // --- Fire Bomb ---
        int FireBombMaxFlightDistance { get; }
        int FireBombEffectRange { get; }
        int FireBombContactDamage { get; }
        int FireBombDotDamage { get; }
        float FireBombDotInterval { get; }
        float FireBombDuration { get; }
        float FireBombCooldown { get; }
        float FireBombCooldownMin { get; }
        float FireBombCooldownReduction { get; }
        bool FireBombDefaultWallPenetration { get; }

        // --- Stage ---
        int StageSize { get; }
        float WallSeedPercent { get; }
        float WallGrowthChance { get; }
        float WallTargetPercent { get; }
        int SpawnProtectionRadius { get; }

        // --- Slime ---
        float SlimeSpawnCheckInterval { get; }
        float SlimeTargetRatio { get; }
        int SlimeMinDistanceFromPlayer { get; }
        int SlimeHp { get; }
        float SlimeSpeedMultiplier { get; }
        int SlimeDetectionRange { get; }
        int SlimeAttackDamage { get; }
        float SlimeAttackCooldown { get; }
        int SlimeSpawnRatioNormal { get; }
        int SlimeSpawnRatioGold { get; }
        int SlimeSpawnRatioRed { get; }

        // --- Match Flow ---
        float PhaseDuration { get; }
        float UpgradeSelectionTimeout { get; }
        int UpgradeChoiceCount { get; }
        int RerollCost { get; }

        // --- Forced Move ---
        float ForcedMoveDuration { get; }
        float InvulnerabilityDuration { get; }

        // --- Bomb Flight ---
        float BombFlightSpeed { get; }
        int BombMinFlightDistance { get; }
        float StageShrinkAnimDuration { get; }

        // --- Bomb Effect Spread ---
        float FireBombSpreadInterval { get; }
        float FallBombSpreadInterval { get; }

        // --- Upgrade: HP Recovery ---
        int HpRecoveryAmount { get; }
        int HpRecoveryThreshold { get; }
    }
}
