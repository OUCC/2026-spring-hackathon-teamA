using NUnit.Framework;
using FloorBreaker.Shared.Domain.Grid;
using FloorBreaker.Shared.Domain.Primitives;
using FloorBreaker.Player.Domain;
using FloorBreaker.Stage.Domain;

namespace FloorBreaker.Tests.EditMode.Player
{
    [TestFixture]
    public class PlayerMoveServiceTests
    {
        private StageModel _stage;
        private PlayerModel _player;
        private PlayerMoveService _svc;

        [SetUp]
        public void SetUp()
        {
            _stage = new StageModel(TileCoordRange.FromSize(10));
            var stats = new PlayerStats(10, 1f, 2f);
            var build = new PlayerBuild(3, 1, 1, 2f, 3.5f, false, 0.5f, 3, 1, 2, 4f, 3f, 1f);
            _player = new PlayerModel(PlayerId.Player1, new GridPos(5, 5), stats, build);
            _svc = new PlayerMoveService();
        }

        [TearDown]
        public void TearDown()
        {
            _player.Dispose();
            _stage.Dispose();
        }

        [Test]
        public void TryMove_North_Succeeds()
        {
            Assert.IsTrue(_svc.TryMove(_player, Direction8.N, _stage));
            Assert.AreEqual(new GridPos(5, 6), _player.CurrentPosition);
        }

        [Test]
        public void TryMove_Diagonal_Succeeds()
        {
            Assert.IsTrue(_svc.TryMove(_player, Direction8.NE, _stage));
            Assert.AreEqual(new GridPos(6, 6), _player.CurrentPosition);
        }

        [Test]
        public void TryMove_IntoWall_Fails()
        {
            _stage.SetTileState(new GridPos(5, 6), TileState.Wall);
            Assert.IsFalse(_svc.TryMove(_player, Direction8.N, _stage));
            Assert.AreEqual(new GridPos(5, 5), _player.CurrentPosition);
        }

        [Test]
        public void TryMove_OutOfBounds_Fails()
        {
            var stats = new PlayerStats(10, 1f, 2f);
            var build = new PlayerBuild(3, 1, 1, 2f, 3.5f, false, 0.5f, 3, 1, 2, 4f, 3f, 1f);
            var edgePlayer = new PlayerModel(PlayerId.Player1, new GridPos(0, 0), stats, build);

            Assert.IsFalse(_svc.TryMove(edgePlayer, Direction8.S, _stage));
            edgePlayer.Dispose();
        }

        [Test]
        public void TryMove_DuringForcedMove_Fails()
        {
            _player.ForcedMove.Start(new GridPos(7, 7), 1f);
            Assert.IsFalse(_svc.TryMove(_player, Direction8.N, _stage));
        }

        [Test]
        public void TryMove_UpdatesFacingDirection()
        {
            _svc.TryMove(_player, Direction8.W, _stage);
            Assert.AreEqual(Direction8.W, _player.CurrentFacing);
        }

        [Test]
        public void TryMove_UpdatesFacingEvenOnFailure()
        {
            _stage.SetTileState(new GridPos(5, 6), TileState.Wall);
            _svc.TryMove(_player, Direction8.N, _stage);
            Assert.AreEqual(Direction8.N, _player.CurrentFacing);
        }
    }
}
