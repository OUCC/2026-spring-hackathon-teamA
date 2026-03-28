using System.Linq;
using NUnit.Framework;
using FloorBreaker.Shared.Domain.Grid;
using FloorBreaker.Stage.Domain;
using FloorBreaker.Bombs.Domain;

namespace FloorBreaker.Tests.EditMode.Bombs
{
    [TestFixture]
    public class BombAreaResolverTests
    {
        private StageModel _stage;
        private BombAreaResolver _resolver;

        [SetUp]
        public void SetUp()
        {
            _stage = new StageModel(TileCoordRange.FromSize(10));
            var queryService = new StageQueryService(_stage);
            _resolver = new BombAreaResolver(queryService);
        }

        [TearDown]
        public void TearDown()
        {
            _stage.Dispose();
        }

        [Test]
        public void Resolve_Range1_Returns5Tiles()
        {
            var center = new GridPos(5, 5);
            var tiles = _resolver.Resolve(center, 1, false);

            Assert.AreEqual(5, tiles.Count);
            Assert.Contains(center, (System.Collections.ICollection)tiles);
            Assert.Contains(new GridPos(5, 6), (System.Collections.ICollection)tiles);
            Assert.Contains(new GridPos(6, 5), (System.Collections.ICollection)tiles);
            Assert.Contains(new GridPos(5, 4), (System.Collections.ICollection)tiles);
            Assert.Contains(new GridPos(4, 5), (System.Collections.ICollection)tiles);
        }

        [Test]
        public void Resolve_WallPenetration_ContinuesThroughWall()
        {
            _stage.SetTileData(new GridPos(6, 5), new TileData { Type = TileType.Wall, Condition = TileCondition.Intact, WarpPairId = -1 });
            var tiles = _resolver.Resolve(new GridPos(5, 5), 3, true);

            Assert.Contains(new GridPos(6, 5), (System.Collections.ICollection)tiles);
            Assert.Contains(new GridPos(7, 5), (System.Collections.ICollection)tiles);
        }

        [Test]
        public void Resolve_NoPenetration_StopsAtWall()
        {
            _stage.SetTileData(new GridPos(6, 5), new TileData { Type = TileType.Wall, Condition = TileCondition.Intact, WarpPairId = -1 });
            var tiles = _resolver.Resolve(new GridPos(5, 5), 3, false);

            Assert.Contains(new GridPos(6, 5), (System.Collections.ICollection)tiles);
            Assert.IsFalse(tiles.Contains(new GridPos(7, 5)));
        }

        [Test]
        public void Resolve_Range2_Returns9Tiles()
        {
            var tiles = _resolver.Resolve(new GridPos(5, 5), 2, false);
            Assert.AreEqual(9, tiles.Count);
        }
    }
}
