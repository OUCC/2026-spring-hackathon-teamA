using NUnit.Framework;
using FloorBreaker.Shared.Domain.Primitives;
using FloorBreaker.Bombs.Domain;

namespace FloorBreaker.Tests.EditMode.Bombs
{
    [TestFixture]
    public class BombCooldownStateTests
    {
        private BombCooldownState _state;

        [SetUp]
        public void SetUp()
        {
            _state = new BombCooldownState();
        }

        [TearDown]
        public void TearDown()
        {
            _state.Dispose();
        }

        [Test]
        public void CanFire_WhenNoCooldown_ReturnsTrue()
        {
            Assert.IsTrue(_state.CanFire(BombType.Break));
            Assert.IsTrue(_state.CanFire(BombType.Fire));
        }

        [Test]
        public void StartCooldown_ThenCanFire_ReturnsFalse()
        {
            _state.StartCooldown(BombType.Break, 4f);
            Assert.IsFalse(_state.CanFire(BombType.Break));
        }

        [Test]
        public void Tick_ReducesCooldown_EventuallyCanFire()
        {
            _state.StartCooldown(BombType.Break, 2f);
            _state.Tick(1f);
            Assert.IsFalse(_state.CanFire(BombType.Break));

            _state.Tick(1f);
            Assert.IsTrue(_state.CanFire(BombType.Break));
        }

        [Test]
        public void StartCooldown_Break_DoesNotAffectFire()
        {
            _state.StartCooldown(BombType.Break, 4f);
            Assert.IsTrue(_state.CanFire(BombType.Fire));
        }

        [Test]
        public void StartCooldown_Fire_DoesNotAffectBreak()
        {
            _state.StartCooldown(BombType.Fire, 2f);
            Assert.IsTrue(_state.CanFire(BombType.Break));
        }

        [Test]
        public void Tick_BothCooldowns_DecrementIndependently()
        {
            _state.StartCooldown(BombType.Break, 4f);
            _state.StartCooldown(BombType.Fire, 2f);

            _state.Tick(2f);
            Assert.IsFalse(_state.CanFire(BombType.Break));
            Assert.IsTrue(_state.CanFire(BombType.Fire));

            _state.Tick(2f);
            Assert.IsTrue(_state.CanFire(BombType.Break));
        }

        [Test]
        public void Tick_DoesNotGoNegative()
        {
            _state.StartCooldown(BombType.Break, 1f);
            _state.Tick(5f);
            Assert.AreEqual(0f, _state.BreakBombRemaining.CurrentValue);
        }

        [Test]
        public void ReactiveProperty_UpdatesOnTick()
        {
            _state.StartCooldown(BombType.Fire, 3f);
            Assert.AreEqual(3f, _state.FireBombRemaining.CurrentValue);

            _state.Tick(1f);
            Assert.AreEqual(2f, _state.FireBombRemaining.CurrentValue);
        }
    }
}
