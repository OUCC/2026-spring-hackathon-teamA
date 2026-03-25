using NUnit.Framework;
using FloorBreaker.Shared.Domain.Grid;

namespace FloorBreaker.Tests.EditMode
{
    [TestFixture]
    public class Direction8Tests
    {
        [TestCase(Direction8.N, 0, 1)]
        [TestCase(Direction8.NE, 1, 1)]
        [TestCase(Direction8.E, 1, 0)]
        [TestCase(Direction8.SE, 1, -1)]
        [TestCase(Direction8.S, 0, -1)]
        [TestCase(Direction8.SW, -1, -1)]
        [TestCase(Direction8.W, -1, 0)]
        [TestCase(Direction8.NW, -1, 1)]
        public void ToOffset_ReturnsCorrectOffset(Direction8 dir, int expectedX, int expectedY)
        {
            var offset = dir.ToOffset();
            Assert.AreEqual(expectedX, offset.X);
            Assert.AreEqual(expectedY, offset.Y);
        }

        [TestCase(Direction8.N, Direction8.S)]
        [TestCase(Direction8.NE, Direction8.SW)]
        [TestCase(Direction8.E, Direction8.W)]
        [TestCase(Direction8.SE, Direction8.NW)]
        public void Opposite_ReturnsCorrectDirection(Direction8 dir, Direction8 expected)
        {
            Assert.AreEqual(expected, dir.Opposite());
            Assert.AreEqual(dir, expected.Opposite());
        }

        [Test]
        public void IsCardinal_CorrectForAllDirections()
        {
            Assert.IsTrue(Direction8.N.IsCardinal());
            Assert.IsFalse(Direction8.NE.IsCardinal());
            Assert.IsTrue(Direction8.E.IsCardinal());
            Assert.IsFalse(Direction8.SE.IsCardinal());
            Assert.IsTrue(Direction8.S.IsCardinal());
            Assert.IsFalse(Direction8.SW.IsCardinal());
            Assert.IsTrue(Direction8.W.IsCardinal());
            Assert.IsFalse(Direction8.NW.IsCardinal());
        }
    }

    [TestFixture]
    public class CardinalDirection4Tests
    {
        [TestCase(CardinalDirection4.N, CardinalDirection4.S)]
        [TestCase(CardinalDirection4.E, CardinalDirection4.W)]
        public void Opposite_ReturnsCorrectDirection(CardinalDirection4 dir, CardinalDirection4 expected)
        {
            Assert.AreEqual(expected, dir.Opposite());
            Assert.AreEqual(dir, expected.Opposite());
        }

        [Test]
        public void ToDirection8_Converts()
        {
            Assert.AreEqual(Direction8.N, CardinalDirection4.N.ToDirection8());
            Assert.AreEqual(Direction8.E, CardinalDirection4.E.ToDirection8());
            Assert.AreEqual(Direction8.S, CardinalDirection4.S.ToDirection8());
            Assert.AreEqual(Direction8.W, CardinalDirection4.W.ToDirection8());
        }
    }
}
