using System.Linq;
using NUnit.Framework;
using FloorBreaker.Shared.Domain.Grid;
using FloorBreaker.Shared.Domain.Primitives;
using FloorBreaker.Shared.Application.Interfaces;
using FloorBreaker.Player.Domain;
using FloorBreaker.Upgrades.Application;

namespace FloorBreaker.Tests.EditMode.Player
{
    [TestFixture]
    public class PlayerBuildTests
    {
        private PlayerModel _player;
        private UpgradeApplyService _applyService;

        private PlayerModel CreateDefaultPlayer()
        {
            var build = new PlayerBuild(
                fireFlightRange: 3, fireEffectRange: 1, fireDamage: 1, fireCooldown: 2f,
                fireDuration: 3.5f, fireWallPenetration: false, fireCooldownMin: 0.5f,
                breakFlightRange: 3, breakEffectRange: 1, breakDamage: 2, breakCooldown: 4f,
                breakCollapseTime: 3f, breakCooldownMin: 1f);
            var stats = new PlayerStats(10, 1f, 3f);
            return new PlayerModel(PlayerId.Player1, new GridPos(5, 5), stats, build);
        }

        [SetUp]
        public void SetUp()
        {
            _applyService = new UpgradeApplyService(new TestBalanceParameters());
        }

        [TearDown]
        public void TearDown()
        {
            _player?.Dispose();
        }

        private void ApplyUpgrade(UpgradeId id)
        {
            _applyService.Apply(id, _player);
        }

        [Test]
        public void ApplyUpgrade_FireFlightRange_Increments()
        {
            _player = CreateDefaultPlayer();
            ApplyUpgrade(UpgradeId.FireFlightRange);
            Assert.AreEqual(5, _player.Build.FireFlightRange);
        }

        [Test]
        public void ApplyUpgrade_FireCooldown_RespectsMin()
        {
            _player = CreateDefaultPlayer(); // CD = 2.0, min = 0.5
            // 5 reductions: 2.0 -> 1.7 -> 1.4 -> 1.1 -> 0.8 -> 0.5
            for (int i = 0; i < 10; i++)
                ApplyUpgrade(UpgradeId.FireCooldown);

            Assert.AreEqual(0.5f, _player.Build.FireCooldown, 0.01f);
        }

        [Test]
        public void ApplyUpgrade_BreakCooldown_RespectsMin()
        {
            _player = CreateDefaultPlayer(); // CD = 4.0, min = 1.0
            for (int i = 0; i < 20; i++)
                ApplyUpgrade(UpgradeId.BreakCooldown);

            Assert.AreEqual(1.0f, _player.Build.BreakCooldown, 0.01f);
        }

        [Test]
        public void ApplyUpgrade_FireWallPenetration_SetTrue()
        {
            _player = CreateDefaultPlayer();
            Assert.IsFalse(_player.Build.FireWallPenetration);
            ApplyUpgrade(UpgradeId.FireWallPenetration);
            Assert.IsTrue(_player.Build.FireWallPenetration);
        }

        [Test]
        public void ApplyUpgrade_BreakCollapseTime_Increases()
        {
            _player = CreateDefaultPlayer();
            ApplyUpgrade(UpgradeId.BreakCollapseTime);
            Assert.AreEqual(5f, _player.Build.BreakCollapseTime, 0.01f);
        }

        // --- AcquiredUpgrades tracking tests ---

        [Test]
        public void RecordUpgrade_TracksInAcquiredList()
        {
            _player = CreateDefaultPlayer();
            _player.Build.RecordUpgrade(UpgradeId.MoveSpeed);

            var acquired = _player.Build.AcquiredUpgrades.CurrentValue;
            Assert.AreEqual(1, acquired.Count);
            Assert.AreEqual(UpgradeId.MoveSpeed, acquired[0]);
        }

        [Test]
        public void ApplyUpgrade_BombUpgrade_AlsoRecordsInAcquiredList()
        {
            _player = CreateDefaultPlayer();
            ApplyUpgrade(UpgradeId.FireFlightRange);

            var acquired = _player.Build.AcquiredUpgrades.CurrentValue;
            Assert.AreEqual(1, acquired.Count);
            Assert.AreEqual(UpgradeId.FireFlightRange, acquired[0]);
        }

        [Test]
        public void AcquiredUpgrades_MultipleUpgrades_TracksAllInOrder()
        {
            _player = CreateDefaultPlayer();
            ApplyUpgrade(UpgradeId.FireDamage);
            ApplyUpgrade(UpgradeId.BreakEffectRange);
            ApplyUpgrade(UpgradeId.HpRecovery);

            var acquired = _player.Build.AcquiredUpgrades.CurrentValue;
            Assert.AreEqual(3, acquired.Count);
            Assert.AreEqual(UpgradeId.FireDamage, acquired[0]);
            Assert.AreEqual(UpgradeId.BreakEffectRange, acquired[1]);
            Assert.AreEqual(UpgradeId.HpRecovery, acquired[2]);
        }

        [Test]
        public void AcquiredUpgrades_InitiallyEmpty()
        {
            _player = CreateDefaultPlayer();
            Assert.AreEqual(0, _player.Build.AcquiredUpgrades.CurrentValue.Count);
        }

        private class TestBalanceParameters : IBalanceParameters
        {
            public int InitialHp => 10;
            public float BaseMovementSpeed => 1f;
            public float MaxMovementSpeed => 3f;
            public float MovementSpeedIncrement => 0.2f;
            public int BreakBombMaxFlightDistance => 3;
            public int BreakBombEffectRange => 1;
            public int BreakBombDamage => 2;
            public float BreakBombCollapseDuration => 3f;
            public float BreakBombRecoveryDuration => 5f;
            public float BreakBombCooldown => 4f;
            public float BreakBombCooldownMin => 1f;
            public float BreakBombCooldownReduction => 0.5f;
            public bool BreakBombDefaultWallPenetration => false;
            public int FireBombMaxFlightDistance => 3;
            public int FireBombEffectRange => 1;
            public int FireBombContactDamage => 1;
            public int FireBombDotDamage => 1;
            public float FireBombDotInterval => 1f;
            public float FireBombDuration => 3.5f;
            public float FireBombCooldown => 2f;
            public float FireBombCooldownMin => 0.5f;
            public float FireBombCooldownReduction => 0.3f;
            public bool FireBombDefaultWallPenetration => false;
            public int StageSize => 30;
            public float WallSeedPercent => 0.08f;
            public float WallGrowthChance => 0.4f;
            public float WallTargetPercent => 0.15f;
            public int SpawnProtectionRadius => 2;
            public float SlimeSpawnCheckInterval => 5f;
            public float SlimeTargetRatio => 0.03f;
            public int SlimeMinDistanceFromPlayer => 5;
            public int SlimeHp => 1;
            public float SlimeSpeedMultiplier => 0.5f;
            public int SlimeDetectionRange => 5;
            public int SlimeAttackDamage => 1;
            public float SlimeAttackCooldown => 1f;
            public int SlimeSpawnRatioNormal => 10;
            public int SlimeSpawnRatioGold => 1;
            public int SlimeSpawnRatioRed => 1;
            public float PhaseDuration => 20f;
            public float UpgradeSelectionTimeout => 10f;
            public int UpgradeChoiceCount => 3;
            public int RerollCost => 1;
            public float ForcedMoveDuration => 1f;
            public int HpRecoveryAmount => 3;
            public int HpRecoveryThreshold => 5;
            public float InvulnerabilityDuration => 1.5f;
            public float BombFlightSpeed => 12f;
            public int BombMinFlightDistance => 3;
            public float StageShrinkAnimDuration => 1f;
            public float FireBombSpreadInterval => 0.15f;
            public float BreakBombSpreadInterval => 0.3f;
            public float DashCooldown => 1f;
            public float DashDoubleTapWindow => 0.3f;
            public int FireFlightRangeIncrement => 2;
            public int FireEffectRangeIncrement => 1;
            public int FireDamageIncrement => 1;
            public float FireDurationIncrement => 2f;
            public float FireCooldownReduction => 0.3f;
            public int BreakFlightRangeIncrement => 2;
            public int BreakEffectRangeIncrement => 1;
            public int BreakDamageIncrement => 1;
            public float BreakCollapseTimeIncrement => 2f;
            public float BreakCooldownReduction => 0.5f;
        }
    }
}
