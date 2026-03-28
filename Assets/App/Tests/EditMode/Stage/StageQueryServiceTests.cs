using System.Linq;
using NUnit.Framework;
using FloorBreaker.Shared.Domain.Grid;
using FloorBreaker.Stage.Domain;

namespace FloorBreaker.Tests.EditMode.Stage
{
    [TestFixture]
    public class StageQueryServiceTests
    {
        [Test]
        public void GetTilesInCross_Range1_Returns5Tiles()
        {
            var model = new StageModel(TileCoordRange.FromSize(10));
            var svc = new StageQueryService(model);
            var center = new GridPos(5, 5);

            var tiles = svc.GetTilesInCross(center, 1, false);

            Assert.AreEqual(5, tiles.Count);
            Assert.Contains(center, (System.Collections.ICollection)tiles);
            Assert.Contains(new GridPos(5, 6), (System.Collections.ICollection)tiles);
            Assert.Contains(new GridPos(6, 5), (System.Collections.ICollection)tiles);
            Assert.Contains(new GridPos(5, 4), (System.Collections.ICollection)tiles);
            Assert.Contains(new GridPos(4, 5), (System.Collections.ICollection)tiles);
        }

        [Test]
        public void GetTilesInCross_WallStopsArm_WhenNotPenetrating()
        {
            var model = new StageModel(TileCoordRange.FromSize(10));
            model.SetTileData(new GridPos(6, 5), new TileData { Type = TileType.Wall, Condition = TileCondition.Intact, WarpPairId = -1 });
            var svc = new StageQueryService(model);

            var tiles = svc.GetTilesInCross(new GridPos(5, 5), 3, false);

            // East arm should stop at the wall (include wall itself), other arms extend 3
            Assert.Contains(new GridPos(6, 5), (System.Collections.ICollection)tiles); // wall hit
            Assert.IsFalse(tiles.Contains(new GridPos(7, 5))); // blocked by wall
        }

        [Test]
        public void GetTilesInCross_WallPenetration_ContinuesThroughWall()
        {
            var model = new StageModel(TileCoordRange.FromSize(10));
            model.SetTileData(new GridPos(6, 5), new TileData { Type = TileType.Wall, Condition = TileCondition.Intact, WarpPairId = -1 });
            var svc = new StageQueryService(model);

            var tiles = svc.GetTilesInCross(new GridPos(5, 5), 3, true);

            Assert.Contains(new GridPos(6, 5), (System.Collections.ICollection)tiles);
            Assert.Contains(new GridPos(7, 5), (System.Collections.ICollection)tiles);
        }

        [Test]
        public void RaycastGrid_StopsAtWall()
        {
            var model = new StageModel(TileCoordRange.FromSize(10));
            model.SetTileData(new GridPos(7, 5), new TileData { Type = TileType.Wall, Condition = TileCondition.Intact, WarpPairId = -1 });
            var svc = new StageQueryService(model);

            var result = svc.RaycastGrid(new GridPos(5, 5), Direction8.E, 5);

            Assert.IsNotNull(result);
            Assert.AreEqual(new GridPos(7, 5), result.Value.HitPos);
            Assert.AreEqual(2, result.Value.Distance);
            Assert.AreEqual(TileType.Wall, result.Value.HitTileData.Type);
        }

        [Test]
        public void RaycastGrid_ReturnsNull_WhenOutOfBounds()
        {
            var model = new StageModel(TileCoordRange.FromSize(5));
            var svc = new StageQueryService(model);

            // Shoot east from edge
            var result = svc.RaycastGrid(new GridPos(4, 2), Direction8.E, 3);
            Assert.IsNull(result);
        }
    }
}
