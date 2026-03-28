using System.Collections.Generic;
using NUnit.Framework;
using R3;
using FloorBreaker.Shared.Domain.Grid;
using FloorBreaker.Slimes.Domain;

namespace FloorBreaker.Tests.EditMode.Slimes
{
    [TestFixture]
    public class SlimeRegistryTests
    {
        private SlimeRegistry _registry;

        [SetUp]
        public void SetUp()
        {
            _registry = new SlimeRegistry();
        }

        [Test]
        public void Add_IncreasesCount()
        {
            var slime = new SlimeModel(new SlimeId(1), SlimeType.Normal, new GridPos(3, 3), 1f);
            _registry.Add(slime);
            Assert.AreEqual(1, _registry.AliveCount);
        }

        [Test]
        public void Remove_DecreasesCount()
        {
            var slime = new SlimeModel(new SlimeId(1), SlimeType.Normal, new GridPos(3, 3), 1f);
            _registry.Add(slime);
            Assert.AreEqual(1, _registry.AliveCount);

            _registry.Remove(slime.Id);
            Assert.AreEqual(0, _registry.AliveCount);
        }

        [Test]
        public void GetAt_ReturnsCorrectSlime()
        {
            var pos = new GridPos(4, 4);
            var slime = new SlimeModel(new SlimeId(1), SlimeType.Gold, pos, 1f);
            _registry.Add(slime);

            var found = _registry.GetAt(pos);
            Assert.IsNotNull(found);
            Assert.AreEqual(slime.Id, found.Id);
        }

        [Test]
        public void GetAt_EmptyPos_ReturnsNull()
        {
            var found = _registry.GetAt(new GridPos(7, 7));
            Assert.IsNull(found);
        }

        [Test]
        public void IsOccupied_True_WhenSlimePresent()
        {
            var pos = new GridPos(2, 2);
            var slime = new SlimeModel(new SlimeId(1), SlimeType.Normal, pos, 1f);
            _registry.Add(slime);

            Assert.IsTrue(_registry.IsOccupied(pos));
            Assert.IsFalse(_registry.IsOccupied(new GridPos(9, 9)));
        }

        [Test]
        public void GetSlimesAt_ReturnsMatchingSlimes()
        {
            var pos1 = new GridPos(1, 1);
            var pos2 = new GridPos(2, 2);
            var pos3 = new GridPos(3, 3);
            _registry.Add(new SlimeModel(new SlimeId(1), SlimeType.Normal, pos1, 1f));
            _registry.Add(new SlimeModel(new SlimeId(2), SlimeType.Gold, pos2, 1f));
            _registry.Add(new SlimeModel(new SlimeId(3), SlimeType.Red, pos3, 1f));

            var positions = new List<GridPos> { pos1, pos3, new GridPos(9, 9) };
            var result = _registry.GetSlimesAt(positions);

            Assert.AreEqual(2, result.Count);
        }

        [Test]
        public void UpdatePosition_MovesIndex()
        {
            var oldPos = new GridPos(1, 1);
            var newPos = new GridPos(2, 2);
            var slime = new SlimeModel(new SlimeId(1), SlimeType.Normal, oldPos, 1f);
            _registry.Add(slime);

            slime.MoveTo(newPos);
            _registry.UpdatePosition(slime, oldPos, newPos);

            Assert.IsNull(_registry.GetAt(oldPos));
            Assert.IsNotNull(_registry.GetAt(newPos));
            Assert.AreEqual(slime.Id, _registry.GetAt(newPos).Id);
        }

        // ─── Event Tests ───────────────────────────────────────

        [Test]
        public void Add_EmitsSpawnedEvent()
        {
            SlimeSpawnedEvent? received = null;
            _registry.Spawned.Subscribe(e => received = e);

            var pos = new GridPos(5, 5);
            var slime = new SlimeModel(new SlimeId(1), SlimeType.Gold, pos, 1f);
            _registry.Add(slime);

            Assert.IsNotNull(received);
            Assert.AreEqual(slime.Id, received.Value.Id);
            Assert.AreEqual(SlimeType.Gold, received.Value.Type);
            Assert.AreEqual(pos, received.Value.Position);
        }

        [Test]
        public void Remove_EmitsKilledEvent()
        {
            SlimeKilledEvent? received = null;
            _registry.Killed.Subscribe(e => received = e);

            var pos = new GridPos(3, 3);
            var slime = new SlimeModel(new SlimeId(1), SlimeType.Red, pos, 1f);
            _registry.Add(slime);

            _registry.Remove(slime.Id);

            Assert.IsNotNull(received);
            Assert.AreEqual(slime.Id, received.Value.Id);
            Assert.AreEqual(SlimeType.Red, received.Value.Type);
            Assert.AreEqual(pos, received.Value.Position);
        }

        [Test]
        public void UpdatePosition_EmitsMovedEvent()
        {
            SlimeMovedEvent? received = null;
            _registry.Moved.Subscribe(e => received = e);

            var oldPos = new GridPos(1, 1);
            var newPos = new GridPos(1, 2);
            var slime = new SlimeModel(new SlimeId(1), SlimeType.Normal, oldPos, 1f);
            _registry.Add(slime);

            slime.MoveTo(newPos);
            _registry.UpdatePosition(slime, oldPos, newPos);

            Assert.IsNotNull(received);
            Assert.AreEqual(slime.Id, received.Value.Id);
            Assert.AreEqual(oldPos, received.Value.OldPosition);
            Assert.AreEqual(newPos, received.Value.NewPosition);
        }

        [Test]
        public void Remove_NonExistent_DoesNotEmitKilledEvent()
        {
            SlimeKilledEvent? received = null;
            _registry.Killed.Subscribe(e => received = e);

            _registry.Remove(new SlimeId(1));

            Assert.IsNull(received);
        }
    }
}
