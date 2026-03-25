using System.Linq;
using NUnit.Framework;
using FloorBreaker.Shared.Domain.Grid;
using FloorBreaker.Shared.Domain.Primitives;
using FloorBreaker.Shared.Application.Interfaces;
using FloorBreaker.Player.Domain;
using FloorBreaker.Upgrades.Domain;

namespace FloorBreaker.Tests.EditMode.Upgrades
{
    [TestFixture]
    public class UpgradeApplyServiceTests
    {
        private PlayerModel _player;
        private UpgradeApplyService _svc;

        [SetUp]
        public void SetUp()
        {
            var stats = new PlayerStats(10, 1f, 2f);
            var build = new PlayerBuild(3, 1, 1, 2f, 3.5f, false, 0.5f, 3, 1, 2, 4f, 3f, 1f);
            _player = new PlayerModel(PlayerId.Player1, new GridPos(5, 5), stats, build);
            _svc = new UpgradeApplyService(new TestBalanceParameters());
        }

        [TearDown]
        public void TearDown()
        {
            _player.Dispose();
        }

        [Test]
        public void Apply_FireDamage_IncrementsValue()
        {
            int before = _player.Build.FireDamage;
            _svc.Apply(UpgradeId.FireDamage, _player);
            Assert.AreEqual(before + 1, _player.Build.FireDamage);
        }

        [Test]
        public void Apply_MoveSpeed_IncrementsCapped()
        {
            _player.Stats.MoveSpeed = 1.9f;
            _svc.Apply(UpgradeId.MoveSpeed, _player);
            // 1.9 + 0.2 = 2.1, capped at MaxMoveSpeed = 2.0
            Assert.AreEqual(2.0f, _player.Stats.MoveSpeed, 0.001f);
        }

        [Test]
        public void Apply_HpRecovery_Heals()
        {
            _player.Stats.TakeDamage(5);
            Assert.AreEqual(5, _player.Stats.CurrentHp.CurrentValue);

            _svc.Apply(UpgradeId.HpRecovery, _player);
            Assert.AreEqual(8, _player.Stats.CurrentHp.CurrentValue);
        }

        [Test]
        public void Apply_FireWallPenetration_SetsTrue()
        {
            Assert.IsFalse(_player.Build.FireWallPenetration);
            _svc.Apply(UpgradeId.FireWallPenetration, _player);
            Assert.IsTrue(_player.Build.FireWallPenetration);
        }

        [Test]
        public void Apply_FallCooldown_RespectsMin()
        {
            // FallCooldown starts at 4f, min is 1f, reduction is 0.5f per apply
            // Apply enough times to hit the min
            for (int i = 0; i < 20; i++)
            {
                _svc.Apply(UpgradeId.FallCooldown, _player);
            }

            Assert.GreaterOrEqual(_player.Build.FallCooldown, _player.Build.FallCooldownMin);
            Assert.AreEqual(_player.Build.FallCooldownMin, _player.Build.FallCooldown, 0.001f);
        }

        [Test]
        public void Apply_MoveSpeed_RecordsInPlayerBuild()
        {
            _svc.Apply(UpgradeId.MoveSpeed, _player);

            var acquired = _player.Build.AcquiredUpgrades.CurrentValue;
            Assert.IsTrue(acquired.Contains(UpgradeId.MoveSpeed));
        }

        [Test]
        public void Apply_HpRecovery_RecordsInPlayerBuild()
        {
            _player.Stats.TakeDamage(5);
            _svc.Apply(UpgradeId.HpRecovery, _player);

            var acquired = _player.Build.AcquiredUpgrades.CurrentValue;
            Assert.IsTrue(acquired.Contains(UpgradeId.HpRecovery));
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
            public float InvulnerabilityDuration => 1.5f;
            public float BombFlightSpeed => 12f;
            public float StageShrinkAnimDuration => 1f;
            public float FireBombSpreadInterval => 0.15f;
            public float FallBombSpreadInterval => 0.3f;
        }
    }
}
