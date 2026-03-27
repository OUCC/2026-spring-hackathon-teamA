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

        // --- Break Bomb ---
        int BreakBombMaxFlightDistance { get; }
        int BreakBombEffectRange { get; }
        int BreakBombDamage { get; }
        float BreakBombCollapseDuration { get; }
        float BreakBombRecoveryDuration { get; }
        float BreakBombCooldown { get; }
        float BreakBombCooldownMin { get; }
        float BreakBombCooldownReduction { get; }
        bool BreakBombDefaultWallPenetration { get; }

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
        float BreakBombSpreadInterval { get; }

        // --- Upgrade: HP Recovery ---
        int HpRecoveryAmount { get; }
        int HpRecoveryThreshold { get; }

        // --- Dash ---
        float DashCooldown { get; }
        float DashDoubleTapWindow { get; }

        // --- Input ---
        float InputBaseMoveInterval { get; }
        float InputInitialRepeatDelay { get; }
        float InputBufferTime { get; }

        // --- CPU AI ---
        float CpuThinkInterval { get; }
        float CpuBaseMoveInterval { get; }
        float CpuBombReleaseDelay { get; }
        float CpuUpgradeInitialDelay { get; }
        float CpuUpgradePurchaseInterval { get; }

        // --- Upgrade Effect Magnitudes ---
        int FireFlightRangeIncrement { get; }
        int FireEffectRangeIncrement { get; }
        int FireDamageIncrement { get; }
        float FireDurationIncrement { get; }
        float FireCooldownReduction { get; }
        int BreakFlightRangeIncrement { get; }
        int BreakEffectRangeIncrement { get; }
        int BreakDamageIncrement { get; }
        float BreakCollapseTimeIncrement { get; }
        float BreakCooldownReduction { get; }
    }
}
