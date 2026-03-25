using System.Linq;
using NUnit.Framework;
using FloorBreaker.Player.Domain;
using FloorBreaker.Shared.Domain.Primitives;

namespace FloorBreaker.Tests.EditMode.Player
{
    [TestFixture]
    public class PlayerBuildTests
    {
        private PlayerBuild _build;

        private PlayerBuild CreateDefault() => new PlayerBuild(
            fireFlightRange: 3, fireEffectRange: 1, fireDamage: 1, fireCooldown: 2f,
            fireDuration: 3.5f, fireWallPenetration: false, fireCooldownMin: 0.5f,
            fallFlightRange: 3, fallEffectRange: 1, fallDamage: 2, fallCooldown: 4f,
            fallCollapseTime: 3f, fallCooldownMin: 1f);

        [Test]
        public void ApplyUpgrade_FireFlightRange_Increments()
        {
            var build = CreateDefault();
            build.ApplyUpgrade(UpgradeId.FireFlightRange);
            Assert.AreEqual(4, build.FireFlightRange);
        }

        [Test]
        public void ApplyUpgrade_FireCooldown_RespectsMin()
        {
            var build = CreateDefault(); // CD = 2.0, min = 0.5
            // 5 reductions: 2.0 -> 1.7 -> 1.4 -> 1.1 -> 0.8 -> 0.5
            for (int i = 0; i < 10; i++)
                build.ApplyUpgrade(UpgradeId.FireCooldown);

            Assert.AreEqual(0.5f, build.FireCooldown, 0.01f);
        }

        [Test]
        public void ApplyUpgrade_FallCooldown_RespectsMin()
        {
            var build = CreateDefault(); // CD = 4.0, min = 1.0
            for (int i = 0; i < 20; i++)
                build.ApplyUpgrade(UpgradeId.FallCooldown);

            Assert.AreEqual(1.0f, build.FallCooldown, 0.01f);
        }

        [Test]
        public void ApplyUpgrade_FireWallPenetration_SetTrue()
        {
            var build = CreateDefault();
            Assert.IsFalse(build.FireWallPenetration);
            build.ApplyUpgrade(UpgradeId.FireWallPenetration);
            Assert.IsTrue(build.FireWallPenetration);
        }

        [Test]
        public void ApplyUpgrade_FallCollapseTime_Increases()
        {
            var build = CreateDefault();
            build.ApplyUpgrade(UpgradeId.FallCollapseTime);
            Assert.AreEqual(5f, build.FallCollapseTime, 0.01f);
            build.Dispose();
        }

        // --- AcquiredUpgrades 追跡テスト ---

        [Test]
        public void RecordUpgrade_TracksInAcquiredList()
        {
            _build = CreateDefault();
            _build.RecordUpgrade(UpgradeId.MoveSpeed);

            var acquired = _build.AcquiredUpgrades.CurrentValue;
            Assert.AreEqual(1, acquired.Count);
            Assert.AreEqual(UpgradeId.MoveSpeed, acquired[0]);
            _build.Dispose();
        }

        [Test]
        public void ApplyUpgrade_BombUpgrade_AlsoRecordsInAcquiredList()
        {
            _build = CreateDefault();
            _build.ApplyUpgrade(UpgradeId.FireFlightRange);

            var acquired = _build.AcquiredUpgrades.CurrentValue;
            Assert.AreEqual(1, acquired.Count);
            Assert.AreEqual(UpgradeId.FireFlightRange, acquired[0]);
            _build.Dispose();
        }

        [Test]
        public void AcquiredUpgrades_MultipleUpgrades_TracksAllInOrder()
        {
            _build = CreateDefault();
            _build.ApplyUpgrade(UpgradeId.FireDamage);
            _build.ApplyUpgrade(UpgradeId.FallEffectRange);
            _build.RecordUpgrade(UpgradeId.HpRecovery);

            var acquired = _build.AcquiredUpgrades.CurrentValue;
            Assert.AreEqual(3, acquired.Count);
            Assert.AreEqual(UpgradeId.FireDamage, acquired[0]);
            Assert.AreEqual(UpgradeId.FallEffectRange, acquired[1]);
            Assert.AreEqual(UpgradeId.HpRecovery, acquired[2]);
            _build.Dispose();
        }

        [Test]
        public void AcquiredUpgrades_InitiallyEmpty()
        {
            _build = CreateDefault();
            Assert.AreEqual(0, _build.AcquiredUpgrades.CurrentValue.Count);
            _build.Dispose();
        }
    }
}
