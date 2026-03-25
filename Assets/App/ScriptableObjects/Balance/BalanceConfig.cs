using UnityEngine;
using FloorBreaker.Shared.Application.Interfaces;

namespace FloorBreaker.ScriptableObjects.Balance
{
    [CreateAssetMenu(fileName = "BalanceConfig", menuName = "FloorBreaker/Balance Config")]
    public sealed class BalanceConfig : ScriptableObject, IBalanceParameters
    {
        [Header("Player")]
        [SerializeField] private int initialHp = 10;
        [SerializeField] private float baseMovementSpeed = 1f;
        [SerializeField] private float maxMovementSpeed = 2f;
        [SerializeField] private float movementSpeedIncrement = 0.2f;

        [Header("Fall Bomb")]
        [SerializeField] private int fallBombMaxFlightDistance = 3;
        [SerializeField] private int fallBombEffectRange = 1;
        [SerializeField] private int fallBombDamage = 2;
        [SerializeField] private float fallBombCollapseDuration = 3f;
        [SerializeField] private float fallBombRecoveryDuration = 5f;
        [SerializeField] private float fallBombCooldown = 4f;
        [SerializeField] private float fallBombCooldownMin = 1f;
        [SerializeField] private float fallBombCooldownReduction = 0.5f;
        [SerializeField] private bool fallBombDefaultWallPenetration = true;

        [Header("Fire Bomb")]
        [SerializeField] private int fireBombMaxFlightDistance = 3;
        [SerializeField] private int fireBombEffectRange = 1;
        [SerializeField] private int fireBombContactDamage = 1;
        [SerializeField] private int fireBombDotDamage = 1;
        [SerializeField] private float fireBombDotInterval = 1f;
        [SerializeField] private float fireBombDuration = 3.5f;
        [SerializeField] private float fireBombCooldown = 2f;
        [SerializeField] private float fireBombCooldownMin = 0.5f;
        [SerializeField] private float fireBombCooldownReduction = 0.3f;
        [SerializeField] private bool fireBombDefaultWallPenetration;

        [Header("Stage")]
        [SerializeField] private int stageSize = 30;
        [SerializeField] private float wallSeedPercent = 0.08f;
        [SerializeField] private float wallGrowthChance = 0.4f;
        [SerializeField] private float wallTargetPercent = 0.2f;
        [SerializeField] private int spawnProtectionRadius = 2;

        [Header("Slime")]
        [SerializeField] private float slimeSpawnCheckInterval = 5f;
        [SerializeField] private float slimeTargetRatio = 0.03f;
        [SerializeField] private int slimeMinDistanceFromPlayer = 5;
        [SerializeField] private int slimeHp = 1;
        [SerializeField] private float slimeSpeedMultiplier = 0.5f;
        [SerializeField] private int slimeDetectionRange = 5;
        [SerializeField] private int slimeAttackDamage = 1;
        [SerializeField] private float slimeAttackCooldown = 1f;
        [SerializeField] private int slimeSpawnRatioNormal = 10;
        [SerializeField] private int slimeSpawnRatioGold = 1;
        [SerializeField] private int slimeSpawnRatioRed = 1;

        [Header("Match Flow")]
        [SerializeField] private float phaseDuration = 20f;
        [SerializeField] private float upgradeSelectionTimeout = 10f;
        [SerializeField] private int upgradeChoiceCount = 3;
        [SerializeField] private int rerollCost = 1;

        [Header("Forced Move / Combat")]
        [SerializeField] private float forcedMoveDuration = 1f;
        [SerializeField] private float invulnerabilityDuration = 1.5f;

        [Header("Bomb Flight")]
        [SerializeField] private float bombFlightSpeed = 12f;
        [SerializeField] private float stageShrinkAnimDuration = 1f;

        [Header("Upgrade: HP Recovery")]
        [SerializeField] private int hpRecoveryAmount = 3;
        [SerializeField] private int hpRecoveryThreshold = 5;

        // --- IBalanceParameters ---
        public int InitialHp => initialHp;
        public float BaseMovementSpeed => baseMovementSpeed;
        public float MaxMovementSpeed => maxMovementSpeed;
        public float MovementSpeedIncrement => movementSpeedIncrement;

        public int FallBombMaxFlightDistance => fallBombMaxFlightDistance;
        public int FallBombEffectRange => fallBombEffectRange;
        public int FallBombDamage => fallBombDamage;
        public float FallBombCollapseDuration => fallBombCollapseDuration;
        public float FallBombRecoveryDuration => fallBombRecoveryDuration;
        public float FallBombCooldown => fallBombCooldown;
        public float FallBombCooldownMin => fallBombCooldownMin;
        public float FallBombCooldownReduction => fallBombCooldownReduction;
        public bool FallBombDefaultWallPenetration => fallBombDefaultWallPenetration;

        public int FireBombMaxFlightDistance => fireBombMaxFlightDistance;
        public int FireBombEffectRange => fireBombEffectRange;
        public int FireBombContactDamage => fireBombContactDamage;
        public int FireBombDotDamage => fireBombDotDamage;
        public float FireBombDotInterval => fireBombDotInterval;
        public float FireBombDuration => fireBombDuration;
        public float FireBombCooldown => fireBombCooldown;
        public float FireBombCooldownMin => fireBombCooldownMin;
        public float FireBombCooldownReduction => fireBombCooldownReduction;
        public bool FireBombDefaultWallPenetration => fireBombDefaultWallPenetration;

        public int StageSize => stageSize;
        public float WallSeedPercent => wallSeedPercent;
        public float WallGrowthChance => wallGrowthChance;
        public float WallTargetPercent => wallTargetPercent;
        public int SpawnProtectionRadius => spawnProtectionRadius;

        public float SlimeSpawnCheckInterval => slimeSpawnCheckInterval;
        public float SlimeTargetRatio => slimeTargetRatio;
        public int SlimeMinDistanceFromPlayer => slimeMinDistanceFromPlayer;
        public int SlimeHp => slimeHp;
        public float SlimeSpeedMultiplier => slimeSpeedMultiplier;
        public int SlimeDetectionRange => slimeDetectionRange;
        public int SlimeAttackDamage => slimeAttackDamage;
        public float SlimeAttackCooldown => slimeAttackCooldown;
        public int SlimeSpawnRatioNormal => slimeSpawnRatioNormal;
        public int SlimeSpawnRatioGold => slimeSpawnRatioGold;
        public int SlimeSpawnRatioRed => slimeSpawnRatioRed;

        public float PhaseDuration => phaseDuration;
        public float UpgradeSelectionTimeout => upgradeSelectionTimeout;
        public int UpgradeChoiceCount => upgradeChoiceCount;
        public int RerollCost => rerollCost;

        public float ForcedMoveDuration => forcedMoveDuration;
        public float InvulnerabilityDuration => invulnerabilityDuration;

        public float BombFlightSpeed => bombFlightSpeed;
        public float StageShrinkAnimDuration => stageShrinkAnimDuration;

        public int HpRecoveryAmount => hpRecoveryAmount;
        public int HpRecoveryThreshold => hpRecoveryThreshold;
    }
}
