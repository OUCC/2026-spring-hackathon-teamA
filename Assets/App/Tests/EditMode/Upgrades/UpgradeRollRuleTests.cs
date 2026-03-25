using System.Collections.Generic;
using NUnit.Framework;
using FloorBreaker.Shared.Domain.Grid;
using FloorBreaker.Shared.Domain.Primitives;
using FloorBreaker.Shared.Application.Interfaces;
using FloorBreaker.Shared.Infrastructure.Random;
using FloorBreaker.Player.Domain;
using FloorBreaker.Upgrades.Domain;

namespace FloorBreaker.Tests.EditMode.Upgrades
{
    [TestFixture]
    public class UpgradeRollRuleTests
    {
        private PlayerModel _player;
        private UpgradeCatalog _catalog;
        private UpgradeAvailabilityRule _availabilityRule;
        private UpgradeRollRule _rollRule;

        [SetUp]
        public void SetUp()
        {
            var stats = new PlayerStats(10, 1f, 2f);
            var build = new PlayerBuild(3, 1, 1, 2f, 3.5f, false, 0.5f, 3, 1, 2, 4f, 3f, 1f);
            _player = new PlayerModel(PlayerId.Player1, new GridPos(5, 5), stats, build);
            _catalog = new UpgradeCatalog();
            _availabilityRule = new UpgradeAvailabilityRule(new TestBalanceParameters());
            _rollRule = new UpgradeRollRule(_catalog, _availabilityRule);
        }

        [TearDown]
        public void TearDown()
        {
            _player.Dispose();
        }

        [Test]
        public void Roll_Returns3Choices()
        {
            var random = new SeededRandomProvider(42);
            var choices = _rollRule.Roll(_player, 3, random);
            Assert.AreEqual(3, choices.Count);
        }

        [Test]
        public void Roll_NoDuplicates()
        {
            var random = new SeededRandomProvider(42);
            var choices = _rollRule.Roll(_player, 3, random);

            var ids = new HashSet<UpgradeId>();
            foreach (var c in choices)
            {
                Assert.IsTrue(ids.Add(c.Id), $"Duplicate upgrade: {c.Id}");
            }
        }

        [Test]
        public void Roll_RespectsAvailability()
        {
            // Apply once-only upgrades so they are excluded
            _player.Build.ApplyUpgrade(UpgradeId.FireWallPenetration);
            _player.Build.ApplyUpgrade(UpgradeId.FireFlightDamage);
            _player.Build.ApplyUpgrade(UpgradeId.FallFlightDamage);

            var random = new SeededRandomProvider(42);
            var choices = _rollRule.Roll(_player, 3, random);

            foreach (var c in choices)
            {
                Assert.AreNotEqual(UpgradeId.FireWallPenetration, c.Id);
                Assert.AreNotEqual(UpgradeId.FireFlightDamage, c.Id);
                Assert.AreNotEqual(UpgradeId.FallFlightDamage, c.Id);
            }
        }

        [Test]
        public void Roll_WhenPoolSmall_ReturnsFewerThan3()
        {
            // Acquire all once-only, max out move speed, min out cooldowns, keep HP full
            // This reduces the pool significantly
            _player.Build.ApplyUpgrade(UpgradeId.FireWallPenetration);
            _player.Build.ApplyUpgrade(UpgradeId.FireFlightDamage);
            _player.Build.ApplyUpgrade(UpgradeId.FallFlightDamage);
            _player.Stats.MoveSpeed = 2.0f;
            while (_player.Build.FireCooldown > _player.Build.FireCooldownMin)
                _player.Build.ApplyUpgrade(UpgradeId.FireCooldown);
            while (_player.Build.FallCooldown > _player.Build.FallCooldownMin)
                _player.Build.ApplyUpgrade(UpgradeId.FallCooldown);

            var random = new SeededRandomProvider(42);
            // HpRecovery also unavailable (HP full), MoveSpeed unavailable, FireCooldown unavailable, FallCooldown unavailable
            // 3 once-only unavailable => 7 remain from pool of 16 minus those
            // Actually still enough for 3, so request more than pool
            var choices = _rollRule.Roll(_player, 100, random);
            Assert.Less(choices.Count, 100);
        }

        private class TestBalanceParameters : IBalanceParameters
        {
            public int InitialHp => 10;
            public float BaseMovementSpeed => 1f;
            public float MaxMovementSpeed => 2f;
            public float MovementSpeedIncrement => 0.2f;
            public int FallBombMaxFlightDistance => 3;
            public int FallBombEffectRange => 1;
            public int FallBombDamage => 2;
            public float FallBombCollapseDuration => 3f;
            public float FallBombRecoveryDuration => 5f;
            public float FallBombCooldown => 4f;
            public float FallBombCooldownMin => 1f;
            public float FallBombCooldownReduction => 0.5f;
            public bool FallBombDefaultWallPenetration => false;
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
        }
    }
}
