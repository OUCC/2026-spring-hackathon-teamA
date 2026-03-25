using System.Linq;
using NUnit.Framework;
using FloorBreaker.Shared.Domain.Grid;

namespace FloorBreaker.Tests.EditMode
{
    [TestFixture]
    public class TileCoordRangeTests
    {
        [Test]
        public void FromSize_Creates30x30()
        {
            var range = TileCoordRange.FromSize(30);
            Assert.AreEqual(0, range.MinX);
            Assert.AreEqual(0, range.MinY);
            Assert.AreEqual(29, range.MaxX);
            Assert.AreEqual(29, range.MaxY);
            Assert.AreEqual(30, range.Width);
            Assert.AreEqual(30, range.Height);
            Assert.AreEqual(900, range.TileCount);
        }

        [Test]
        public void Shrink_ReducesBoundsCorrectly()
        {
            var range = TileCoordRange.FromSize(30);
            var shrunk = range.Shrink(1);
            Assert.AreEqual(1, shrunk.MinX);
            Assert.AreEqual(1, shrunk.MinY);
            Assert.AreEqual(28, shrunk.MaxX);
            Assert.AreEqual(28, shrunk.MaxY);
            Assert.AreEqual(28, shrunk.Width);
            Assert.AreEqual(28, shrunk.Height);
            Assert.AreEqual(784, shrunk.TileCount);
        }

        [Test]
        public void Contains_BoundaryConditions()
        {
            var range = TileCoordRange.FromSize(30);
            Assert.IsTrue(range.Contains(new GridPos(0, 0)));
            Assert.IsTrue(range.Contains(new GridPos(29, 29)));
            Assert.IsTrue(range.Contains(new GridPos(15, 15)));
            Assert.IsFalse(range.Contains(new GridPos(-1, 0)));
            Assert.IsFalse(range.Contains(new GridPos(0, 30)));
            Assert.IsFalse(range.Contains(new GridPos(30, 0)));
        }

        [Test]
        public void GetAllPositions_ReturnsCorrectCount()
        {
            var range = TileCoordRange.FromSize(5);
            var all = range.GetAllPositions().ToList();
            Assert.AreEqual(25, all.Count);
        }

        [Test]
        public void GetOuterRing_30x30_Returns116Tiles()
        {
            var range = TileCoordRange.FromSize(30);
            var ring = range.GetOuterRing();
            // 30x30 outer ring = 4*30 - 4 = 116
            Assert.AreEqual(116, ring.Count);
        }

        [Test]
        public void GetOuterRing_AllOnBorder()
        {
            var range = TileCoordRange.FromSize(10);
            var ring = range.GetOuterRing();
            foreach (var pos in ring)
            {
                bool onBorder = pos.X == 0 || pos.X == 9 || pos.Y == 0 || pos.Y == 9;
                Assert.IsTrue(onBorder, $"{pos} is not on border");
            }
        }

        [Test]
        public void GetOuterRing_NoDuplicates()
        {
            var range = TileCoordRange.FromSize(10);
            var ring = range.GetOuterRing();
            var distinct = ring.Distinct().ToList();
            Assert.AreEqual(ring.Count, distinct.Count);
        }
    }
}
