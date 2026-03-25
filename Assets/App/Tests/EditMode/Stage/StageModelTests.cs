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
                Assert.AreEqual(TileState.Normal, model.GetTileState(pos));
        }

        [Test]
        public void SetTileState_ChangesState()
        {
            var model = CreateModel();
            var pos = new GridPos(3, 4);
            model.SetTileState(pos, TileState.Wall);
            Assert.AreEqual(TileState.Wall, model.GetTileState(pos));
        }

        [Test]
        public void SetTileState_FiresObservable()
        {
            var model = CreateModel();
            var pos = new GridPos(5, 5);
            var events = new List<TileChangedEvent>();
            model.TileChanged.Subscribe(e => events.Add(e));

            model.SetTileState(pos, TileState.OnFire);

            Assert.AreEqual(1, events.Count);
            Assert.AreEqual(pos, events[0].Pos);
            Assert.AreEqual(TileState.Normal, events[0].OldState);
            Assert.AreEqual(TileState.OnFire, events[0].NewState);
        }

        [Test]
        public void SetTileState_SameState_DoesNotFireObservable()
        {
            var model = CreateModel();
            var pos = new GridPos(5, 5);
            var events = new List<TileChangedEvent>();
            model.TileChanged.Subscribe(e => events.Add(e));

            model.SetTileState(pos, TileState.Normal); // already Normal
            Assert.AreEqual(0, events.Count);
        }

        [Test]
        public void IsPassable_NormalAndOnFire_ArePassable()
        {
            var model = CreateModel();
            var pos = new GridPos(3, 3);
            Assert.IsTrue(model.IsPassable(pos));

            model.SetTileState(pos, TileState.OnFire);
            Assert.IsTrue(model.IsPassable(pos));
        }

        [Test]
        public void IsPassable_WallAndCollapsed_AreNotPassable()
        {
            var model = CreateModel();
            var pos = new GridPos(3, 3);

            model.SetTileState(pos, TileState.Wall);
            Assert.IsFalse(model.IsPassable(pos));

            model.SetTileState(pos, TileState.Collapsed);
            Assert.IsFalse(model.IsPassable(pos));
        }

        [Test]
        public void OutOfBounds_ReturnsPermanentlyDestroyed()
        {
            var model = CreateModel();
            Assert.AreEqual(TileState.PermanentlyDestroyed, model.GetTileState(new GridPos(-1, 0)));
            Assert.AreEqual(TileState.PermanentlyDestroyed, model.GetTileState(new GridPos(10, 5)));
        }

        [Test]
        public void GetAliveTileCount_ExcludesPermanentlyDestroyed()
        {
            var model = CreateModel(5); // 25 tiles
            Assert.AreEqual(25, model.GetAliveTileCount());

            model.SetTileState(new GridPos(0, 0), TileState.PermanentlyDestroyed);
            model.SetTileState(new GridPos(1, 1), TileState.PermanentlyDestroyed);
            Assert.AreEqual(23, model.GetAliveTileCount());
        }
    }
}
