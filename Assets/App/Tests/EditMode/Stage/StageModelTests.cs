using System.Collections.Generic;
using NUnit.Framework;
using R3;
using FloorBreaker.Shared.Domain.Grid;
using FloorBreaker.Stage.Domain;

namespace FloorBreaker.Tests.EditMode.Stage
{
    [TestFixture]
    public class StageModelTests
    {
        private StageModel CreateModel(int size = 10)
        {
            return new StageModel(TileCoordRange.FromSize(size));
        }

        [Test]
        public void AllTiles_StartAsNormal()
        {
            var model = CreateModel();
            foreach (var pos in TileCoordRange.FromSize(10).GetAllPositions())
                Assert.AreEqual(TileCondition.Intact, model.GetTileCondition(pos));
        }

        [Test]
        public void SetTileData_Wall_ChangesType()
        {
            var model = CreateModel();
            var pos = new GridPos(3, 4);
            model.SetTileData(pos, new TileData { Type = TileType.Wall, Condition = TileCondition.Intact, WarpPairId = -1 });
            Assert.AreEqual(TileType.Wall, model.GetTileType(pos));
        }

        [Test]
        public void SetTileCondition_FiresObservable()
        {
            var model = CreateModel();
            var pos = new GridPos(5, 5);
            var events = new List<TileChangedEvent>();
            model.TileChanged.Subscribe(e => events.Add(e));

            model.SetTileCondition(pos, TileCondition.OnFire);

            Assert.AreEqual(1, events.Count);
            Assert.AreEqual(pos, events[0].Pos);
            Assert.AreEqual(TileCondition.Intact, events[0].OldCondition);
            Assert.AreEqual(TileCondition.OnFire, events[0].NewCondition);
        }

        [Test]
        public void SetTileCondition_SameCondition_DoesNotFireObservable()
        {
            var model = CreateModel();
            var pos = new GridPos(5, 5);
            var events = new List<TileChangedEvent>();
            model.TileChanged.Subscribe(e => events.Add(e));

            model.SetTileCondition(pos, TileCondition.Intact); // already Intact
            Assert.AreEqual(0, events.Count);
        }

        [Test]
        public void IsPassable_IntactAndOnFire_ArePassable()
        {
            var model = CreateModel();
            var pos = new GridPos(3, 3);
            Assert.IsTrue(model.IsPassable(pos));

            model.SetTileCondition(pos, TileCondition.OnFire);
            Assert.IsTrue(model.IsPassable(pos));
        }

        [Test]
        public void IsPassable_WallAndCollapsed_AreNotPassable()
        {
            var model = CreateModel();
            var pos = new GridPos(3, 3);

            model.SetTileData(pos, new TileData { Type = TileType.Wall, Condition = TileCondition.Intact, WarpPairId = -1 });
            Assert.IsFalse(model.IsPassable(pos));

            model.SetTileCondition(pos, TileCondition.Collapsed);
            Assert.IsFalse(model.IsPassable(pos));
        }

        [Test]
        public void OutOfBounds_ReturnsPermanentlyDestroyed()
        {
            var model = CreateModel();
            Assert.AreEqual(TileCondition.PermanentlyDestroyed, model.GetTileCondition(new GridPos(-1, 0)));
            Assert.AreEqual(TileCondition.PermanentlyDestroyed, model.GetTileCondition(new GridPos(10, 5)));
        }

        [Test]
        public void GetAliveTileCount_ExcludesPermanentlyDestroyed()
        {
            var model = CreateModel(5); // 25 tiles
            Assert.AreEqual(25, model.GetAliveTileCount());

            model.SetTileCondition(new GridPos(0, 0), TileCondition.PermanentlyDestroyed);
            model.SetTileCondition(new GridPos(1, 1), TileCondition.PermanentlyDestroyed);
            Assert.AreEqual(23, model.GetAliveTileCount());
        }
    }
}
