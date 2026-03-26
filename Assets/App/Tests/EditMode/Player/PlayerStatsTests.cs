using NUnit.Framework;
using FloorBreaker.Player.Domain;

namespace FloorBreaker.Tests.EditMode.Player
{
    [TestFixture]
    public class PlayerStatsTests
    {
        private PlayerStats Create(int hp = 10) => new PlayerStats(hp, 1f, 3f);

        [Test]
        public void InitialHp_EqualsMax()
        {
            var stats = Create();
            Assert.AreEqual(10, stats.CurrentHp.CurrentValue);
        }

        [Test]
        public void TakeDamage_ReducesHp()
        {
            var stats = Create();
            stats.TakeDamage(3);
            Assert.AreEqual(7, stats.CurrentHp.CurrentValue);
        }

        [Test]
        public void TakeDamage_ClampsToZero()
        {
            var stats = Create();
            stats.TakeDamage(100);
            Assert.AreEqual(0, stats.CurrentHp.CurrentValue);
            Assert.IsTrue(stats.IsDead);
        }

        [Test]
        public void Heal_ClampsToMax()
        {
            var stats = Create();
            stats.TakeDamage(5);
            stats.Heal(100);
            Assert.AreEqual(10, stats.CurrentHp.CurrentValue);
        }

        [Test]
        public void AddCoins_Increases()
        {
            var stats = Create();
            stats.AddCoins(5);
            Assert.AreEqual(5, stats.Coins.CurrentValue);
        }

        [Test]
        public void SpendCoins_Success()
        {
            var stats = Create();
            stats.AddCoins(10);
            Assert.IsTrue(stats.SpendCoins(3));
            Assert.AreEqual(7, stats.Coins.CurrentValue);
        }

        [Test]
        public void SpendCoins_Failure_InsufficientFunds()
        {
            var stats = Create();
            stats.AddCoins(2);
            Assert.IsFalse(stats.SpendCoins(5));
            Assert.AreEqual(2, stats.Coins.CurrentValue);
        }
    }
}
