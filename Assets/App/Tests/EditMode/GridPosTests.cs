using NUnit.Framework;
using FloorBreaker.Shared.Domain.Grid;
using FloorBreaker.Shared.Domain.Primitives;

namespace FloorBreaker.Tests.EditMode
{
    [TestFixture]
    public class GridPosTests
    {
        [Test]
        public void Addition_Works()
        {
            var a = new GridPos(2, 3);
            var b = new GridPos(1, -1);
            Assert.AreEqual(new GridPos(3, 2), a + b);
        }

        [Test]
        public void Subtraction_Works()
        {
            var a = new GridPos(5, 5);
            var b = new GridPos(2, 3);
            Assert.AreEqual(new GridPos(3, 2), a - b);
        }

        [Test]
        public void ScalarMultiplication_Works()
        {
            var a = new GridPos(3, 4);
            Assert.AreEqual(new GridPos(6, 8), a * 2);
        }

        [Test]
        public void ManhattanDistance_IsCorrect()
        {
            var a = new GridPos(0, 0);
            var b = new GridPos(3, 4);
            Assert.AreEqual(7, a.ManhattanDistance(b));
        }

        [Test]
        public void ChebyshevDistance_IsCorrect()
        {
            var a = new GridPos(0, 0);
            var b = new GridPos(3, 4);
            Assert.AreEqual(4, a.ChebyshevDistance(b));
        }

        [Test]
        public void Neighbors4_Returns4Positions()
        {
            var pos = new GridPos(5, 5);
            var n = pos.Neighbors4();
            Assert.AreEqual(4, n.Length);
            Assert.Contains(new GridPos(5, 6), n); // N
            Assert.Contains(new GridPos(6, 5), n); // E
            Assert.Contains(new GridPos(5, 4), n); // S
            Assert.Contains(new GridPos(4, 5), n); // W
        }

        [Test]
        public void Neighbors8_Returns8Positions()
        {
            var pos = new GridPos(5, 5);
            var n = pos.Neighbors8();
            Assert.AreEqual(8, n.Length);
        }

        [Test]
        public void Neighbor_WithDirection_ReturnsCorrectPos()
        {
            var pos = new GridPos(5, 5);
            Assert.AreEqual(new GridPos(5, 6), pos.Neighbor(Direction8.N));
            Assert.AreEqual(new GridPos(6, 6), pos.Neighbor(Direction8.NE));
            Assert.AreEqual(new GridPos(6, 5), pos.Neighbor(Direction8.E));
            Assert.AreEqual(new GridPos(6, 4), pos.Neighbor(Direction8.SE));
            Assert.AreEqual(new GridPos(5, 4), pos.Neighbor(Direction8.S));
            Assert.AreEqual(new GridPos(4, 4), pos.Neighbor(Direction8.SW));
            Assert.AreEqual(new GridPos(4, 5), pos.Neighbor(Direction8.W));
            Assert.AreEqual(new GridPos(4, 6), pos.Neighbor(Direction8.NW));
        }

        [Test]
        public void ToWorldCenter_DefaultTileSize()
        {
            var pos = new GridPos(3, 5);
            var world = pos.ToWorldCenter();
            Assert.AreEqual(3.5f, world.X, 0.001f);
            Assert.AreEqual(5.5f, world.Y, 0.001f);
        }

        [Test]
        public void FromWorld_RoundTrip()
        {
            var original = new GridPos(7, 12);
            var world = original.ToWorldCenter();
            var restored = GridPos.FromWorld(world);
            Assert.AreEqual(original, restored);
        }

        [Test]
        public void Equality_Works()
        {
            Assert.AreEqual(new GridPos(1, 2), new GridPos(1, 2));
            Assert.AreNotEqual(new GridPos(1, 2), new GridPos(2, 1));
            Assert.IsTrue(new GridPos(1, 2) == new GridPos(1, 2));
            Assert.IsTrue(new GridPos(1, 2) != new GridPos(3, 4));
        }

        [Test]
        public void GetHashCode_ConsistentForEqualValues()
        {
            var a = new GridPos(10, 20);
            var b = new GridPos(10, 20);
            Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
        }
    }
}
