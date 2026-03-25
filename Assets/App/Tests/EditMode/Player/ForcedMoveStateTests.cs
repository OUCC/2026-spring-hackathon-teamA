using NUnit.Framework;
using FloorBreaker.Shared.Domain.Grid;
using FloorBreaker.Player.Domain;

namespace FloorBreaker.Tests.EditMode.Player
{
    [TestFixture]
    public class ForcedMoveStateTests
    {
        [Test]
        public void InitialState_NotForced()
        {
            var state = new ForcedMoveState();
            Assert.IsFalse(state.IsForced);
        }

        [Test]
        public void Start_BecomesForced()
        {
            var state = new ForcedMoveState();
            state.Start(new GridPos(5, 5), 1f);
            Assert.IsTrue(state.IsForced);
            Assert.AreEqual(new GridPos(5, 5), state.Target);
        }

        [Test]
        public void Tick_CompletesAfterDuration()
        {
            var state = new ForcedMoveState();
            state.Start(new GridPos(5, 5), 1f);

            state.Tick(0.5f);
            Assert.IsTrue(state.IsForced);

            state.Tick(0.6f);
            Assert.IsFalse(state.IsForced);
        }

        [Test]
        public void Complete_ManualCompletion()
        {
            var state = new ForcedMoveState();
            state.Start(new GridPos(5, 5), 1f);
            state.Complete();
            Assert.IsFalse(state.IsForced);
        }
    }
}
