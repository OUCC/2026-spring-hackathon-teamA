using NUnit.Framework;
using FloorBreaker.Shared.Domain.Grid;
using FloorBreaker.Stage.Domain;

namespace FloorBreaker.Tests.EditMode.Stage
{
    [TestFixture]
    public class StageShrinkServiceTests
    {
        [Test]
        public void ShrinkOuterRing_30x30_Destroys116Tiles()
        {
            var model = new StageModel(TileCoordRange.FromSize(30));
            var svc = new StageShrinkService();
            var destroyed = svc.ShrinkOuterRing(model);

            Assert.AreEqual(116, destroyed.Count);
        }

        [Test]
        public void ShrinkOuterRing_UpdatesBounds()
        {
            var model = new StageModel(TileCoordRange.FromSize(30));
            var svc = new StageShrinkService();
            svc.ShrinkOuterRing(model);

            var bounds = model.GetCurrentBounds();
            Assert.AreEqual(1, bounds.MinX);
            Assert.AreEqual(1, bounds.MinY);
            Assert.AreEqual(28, bounds.MaxX);
            Assert.AreEqual(28, bounds.MaxY);
        }

        [Test]
        public void ShrinkOuterRing_TilesArePermanentlyDestroyed()
        {
            var model = new StageModel(TileCoordRange.FromSize(30));
            var svc = new StageShrinkService();
            var destroyed = svc.ShrinkOuterRing(model);

            foreach (var pos in destroyed)
                Assert.AreEqual(TileState.PermanentlyDestroyed, model.GetTileState(pos));
        }

        [Test]
        public void DoubleShrink_Bounds_2_2_To_27_27()
        {
            var model = new StageModel(TileCoordRange.FromSize(30));
            var svc = new StageShrinkService();
            svc.ShrinkOuterRing(model);
            svc.ShrinkOuterRing(model);

            var bounds = model.GetCurrentBounds();
            Assert.AreEqual(2, bounds.MinX);
            Assert.AreEqual(2, bounds.MinY);
            Assert.AreEqual(27, bounds.MaxX);
            Assert.AreEqual(27, bounds.MaxY);
        }
    }
}
