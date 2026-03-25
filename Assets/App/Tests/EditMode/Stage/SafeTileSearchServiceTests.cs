using System.Collections.Generic;
using NUnit.Framework;
using FloorBreaker.Shared.Domain.Grid;
using FloorBreaker.Stage.Domain;

namespace FloorBreaker.Tests.EditMode.Stage
{
    [TestFixture]
    public class SafeTileSearchServiceTests
    {
        [Test]
        public void FindsAdjacentSafeTile()
        {
            var model = new StageModel(TileCoordRange.FromSize(10));
            var from = new GridPos(5, 5);
            model.SetTileState(from, TileState.Collapsing);

            var svc = new SafeTileSearchService();
            var result = svc.FindSafeTile(model, from, new HashSet<GridPos>());

            Assert.IsNotNull(result);
            Assert.IsTrue(model.IsPassable(result.Value));
        }

        [Test]
        public void BfsFallback_WhenSurroundedByWalls()
        {
            var model = new StageModel(TileCoordRange.FromSize(10));
            var from = new GridPos(5, 5);
            model.SetTileState(from, TileState.Collapsing);

            // 周囲4方向を壁にする
            foreach (var n in from.Neighbors4())
                model.SetTileState(n, TileState.Wall);

            var svc = new SafeTileSearchService();
            var result = svc.FindSafeTile(model, from, new HashSet<GridPos>());

            // BFS でもっと遠い安全マスを見つけるはず
            Assert.IsNotNull(result);
            Assert.IsTrue(model.IsPassable(result.Value));
            Assert.Greater(from.ManhattanDistance(result.Value), 1);
        }

        [Test]
        public void ReturnsNull_WhenNoSafeTile()
        {
            var model = new StageModel(TileCoordRange.FromSize(3));
            // 全タイルを壁にする
            foreach (var pos in TileCoordRange.FromSize(3).GetAllPositions())
                model.SetTileState(pos, TileState.Wall);

            var svc = new SafeTileSearchService();
            var result = svc.FindSafeTile(model, new GridPos(1, 1), new HashSet<GridPos>());

            Assert.IsNull(result);
        }

        [Test]
        public void OccupiedTiles_AreExcluded()
        {
            var model = new StageModel(TileCoordRange.FromSize(5));
            var from = new GridPos(2, 2);
            model.SetTileState(from, TileState.Collapsing);

            // 全隣接を occupied にする
            var occupied = new HashSet<GridPos>(from.Neighbors4());

            var svc = new SafeTileSearchService();
            var result = svc.FindSafeTile(model, from, occupied);

            Assert.IsNotNull(result);
            Assert.IsFalse(occupied.Contains(result.Value));
        }
    }
}
