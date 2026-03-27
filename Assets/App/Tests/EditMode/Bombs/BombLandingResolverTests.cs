using NUnit.Framework;
using FloorBreaker.Shared.Domain.Grid;
using FloorBreaker.Shared.Domain.Primitives;
using FloorBreaker.Stage.Domain;
using FloorBreaker.Bombs.Domain;

namespace FloorBreaker.Tests.EditMode.Bombs
{
    [TestFixture]
    public class BombLandingResolverTests
    {
        private StageModel _stage;
        private BombLandingResolver _resolver;

        [SetUp]
        public void SetUp()
        {
            _stage = new StageModel(TileCoordRange.FromSize(10));
            _resolver = new BombLandingResolver(_stage);
        }

        [TearDown]
        public void TearDown()
        {
            _stage.Dispose();
        }

        private BombFlightCommand MakeCmd(GridPos origin, Direction8 dir, int maxFlight = 3)
        {
            var spec = new BombSpec(BombType.Break, maxFlight, 3, 1, 2, 4f, true, 0f, 3f, 5f);
            return new BombFlightCommand(origin, dir, spec, PlayerId.Player1);
        }

        [Test]
        public void Resolve_NoObstacle_LandsAtMaxDistance()
        {
            var cmd = MakeCmd(new GridPos(2, 5), Direction8.E, 3);
            var pos = _resolver.Resolve(cmd, 3, null);
            Assert.AreEqual(new GridPos(5, 5), pos);
        }

        [Test]
        public void Resolve_WallAt2_LandsAtWall()
        {
            _stage.SetTileState(new GridPos(4, 5), TileState.Wall);
            var cmd = MakeCmd(new GridPos(2, 5), Direction8.E, 3);
            var pos = _resolver.Resolve(cmd, 3, null);
            Assert.AreEqual(new GridPos(4, 5), pos);
        }

        [Test]
        public void Resolve_EntityAt1_LandsAtEntity()
        {
            var cmd = MakeCmd(new GridPos(2, 5), Direction8.E, 3);
            var pos = _resolver.Resolve(cmd, 3, p => p.Equals(new GridPos(3, 5)));
            Assert.AreEqual(new GridPos(3, 5), pos);
        }

        [Test]
        public void Resolve_OutOfBounds_LandsAtLastValid()
        {
            var cmd = MakeCmd(new GridPos(8, 5), Direction8.E, 5);
            var pos = _resolver.Resolve(cmd, 5, null);
            Assert.AreEqual(new GridPos(9, 5), pos);
        }

        [Test]
        public void Resolve_ActualDistanceLessThanMax_LandsAtActualDistance()
        {
            var cmd = MakeCmd(new GridPos(2, 5), Direction8.E, 5);
            var pos = _resolver.Resolve(cmd, 2, null);
            Assert.AreEqual(new GridPos(4, 5), pos);
        }

        [Test]
        public void Resolve_CollapsedTile_PassesThrough()
        {
            // Collapsed タイルはボムが飛び越える
            _stage.SetTileState(new GridPos(4, 5), TileState.Collapsed);
            var cmd = MakeCmd(new GridPos(2, 5), Direction8.E, 3);
            var pos = _resolver.Resolve(cmd, 3, null);
            // (4, 5) を飛び越えて (5, 5) に着弾
            Assert.AreEqual(new GridPos(5, 5), pos);
        }

        [Test]
        public void Resolve_DiagonalDirection_WorksCorrectly()
        {
            var cmd = MakeCmd(new GridPos(2, 2), Direction8.NE, 3);
            var pos = _resolver.Resolve(cmd, 3, null);
            Assert.AreEqual(new GridPos(5, 5), pos);
        }

        [Test]
        public void Resolve_WallBeforeEntity_StopsAtWall()
        {
            _stage.SetTileState(new GridPos(3, 5), TileState.Wall);
            var cmd = MakeCmd(new GridPos(2, 5), Direction8.E, 3);
            var pos = _resolver.Resolve(cmd, 3, p => p.Equals(new GridPos(4, 5)));
            Assert.AreEqual(new GridPos(3, 5), pos);
        }
    }
}
