using NUnit.Framework;
using FloorBreaker.Shared.Domain.Grid;
using FloorBreaker.Shared.Domain.Primitives;
using FloorBreaker.Shared.Application.Interfaces;
using FloorBreaker.Player.Domain;
using FloorBreaker.Stage.Domain;
using FloorBreaker.Upgrades.Domain;
using FloorBreaker.Upgrades.Application;

namespace FloorBreaker.Tests.EditMode.Upgrades
{
    [TestFixture]
    public class UpgradeAvailabilityRuleTests
    {
        private PlayerModel _player;
        private UpgradeCatalog _catalog;
        private UpgradeAvailabilityRule _rule;
        private UpgradeApplyService _applyService;

        [SetUp]
        public void SetUp()
        {
            var balance = new TestBalanceParameters();
            var stats = new PlayerStats(10, 1f, 3f);
            var build = new PlayerBuild(3, 1, 1, 2f, 3.5f, false, 0.5f, 3, 1, 2, 4f, 3f, 1f);
            _player = new PlayerModel(PlayerId.Player1, new GridPos(5, 5), stats, build);
            _catalog = new UpgradeCatalog();
            _rule = new UpgradeAvailabilityRule(balance);
            _applyService = new UpgradeApplyService(balance);
        }

        [TearDown]
        public void TearDown()
        {
            _player.Dispose();
        }

        [Test]
        public void IsAvailable_HpRecovery_WhenHpAboveThreshold_ReturnsFalse()
        {
            var def = _catalog.GetById(UpgradeId.HpRecovery);
            Assert.IsFalse(_rule.IsAvailable(def, _player));
        }

        [Test]
        public void IsAvailable_HpRecovery_WhenHpAtThreshold_ReturnsTrue()
        {
            _player.Stats.TakeDamage(5);
            Assert.AreEqual(5, _player.Stats.CurrentHp.CurrentValue);

            var def = _catalog.GetById(UpgradeId.HpRecovery);
            Assert.IsTrue(_rule.IsAvailable(def, _player));
        }

        [Test]
        public void IsAvailable_OnceOnly_WhenAlreadyAcquired_ReturnsFalse()
        {
            _applyService.Apply(UpgradeId.FireWallPenetration, _player);

            var def = _catalog.GetById(UpgradeId.FireWallPenetration);
            Assert.IsFalse(_rule.IsAvailable(def, _player));
        }

        [Test]
        public void IsAvailable_OnceOnly_WhenNotAcquired_ReturnsTrue()
        {
            var def = _catalog.GetById(UpgradeId.FireWallPenetration);
            Assert.IsTrue(_rule.IsAvailable(def, _player));
        }

        [Test]
        public void IsAvailable_MoveSpeed_WhenAtMax_ReturnsFalse()
        {
            _player.Stats.MoveSpeed = 3.0f;

            var def = _catalog.GetById(UpgradeId.MoveSpeed);
            Assert.IsFalse(_rule.IsAvailable(def, _player));
        }

        [Test]
        public void IsAvailable_FireCooldown_WhenAtMin_ReturnsFalse()
        {
            // Set FireCooldown to min by repeatedly applying
            while (_player.Build.FireCooldown > _player.Build.FireCooldownMin)
            {
                _applyService.Apply(UpgradeId.FireCooldown, _player);
            }

            var def = _catalog.GetById(UpgradeId.FireCooldown);
            Assert.IsFalse(_rule.IsAvailable(def, _player));
        }

        [Test]
        public void IsAvailable_NormalUpgrade_ReturnsTrue()
        {
            var def = _catalog.GetById(UpgradeId.FireDamage);
            Assert.IsTrue(_rule.IsAvailable(def, _player));
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
            public int FireBombMaxFlightDistance => 3;
            public int FireBombEffectRange => 1;
            public int FireBombContactDamage => 1;
            public int FireBombDotDamage => 1;
            public float FireBombDotInterval => 1f;
            public float FireBombDuration => 3.5f;
            public float FireBombCooldown => 2f;
            public float FireBombCooldownMin => 0.5f;
            public bool FireBombDefaultWallPenetration => false;
            public int StageSize => 30;
            public float WallSeedPercent => 0.08f;
            public float WallGrowthChance => 0.4f;
            public float WallTargetPercent => 0.15f;
            public int SpawnProtectionRadius => 2;
            public float SlimeSpawnCheckInterval => 5f;
            public float SlimeTargetRatio => 0.03f;
            public int SlimeMinDistanceFromPlayer => 5;
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
            public float CountdownDuration => 4f;
            public float FireBombSpreadInterval => 0.15f;
            public float BreakBombSpreadInterval => 0.3f;
            public float DashCooldown => 1f;
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
            public float InputBaseMoveInterval => 0.2f;
            public float InputInitialRepeatDelay => 0.15f;
            public float InputBufferTime => 0.04f;
            public float CpuThinkInterval => 0.2f;
            public float CpuBaseMoveInterval => 0.2f;
            public float CpuBombReleaseDelay => 0.08f;
            public float CpuUpgradeInitialDelay => 1.5f;
            public float CpuUpgradePurchaseInterval => 0.6f;
        }
    }
}
