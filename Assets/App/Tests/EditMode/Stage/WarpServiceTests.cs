using NUnit.Framework;
using FloorBreaker.Shared.Domain.Grid;
using FloorBreaker.Stage.Domain;

namespace FloorBreaker.Tests.EditMode.Stage
{
    [TestFixture]
    public class WarpServiceTests
    {
        private StageModel _model;
        private WarpService _warpService;

        [SetUp]
        public void SetUp()
        {
            _model = new StageModel(TileCoordRange.FromSize(10));
            _warpService = new WarpService(_model);
        }

        [TearDown]
        public void TearDown()
        {
            _model.Dispose();
        }

        private void SetWarpPair(GridPos a, GridPos b, short pairId)
        {
            _model.SetTileData(a, new TileData { Type = TileType.Warp, Condition = TileCondition.Intact, WarpPairId = pairId });
            _model.SetTileData(b, new TileData { Type = TileType.Warp, Condition = TileCondition.Intact, WarpPairId = pairId });
            _warpService.BuildRegistry(_model.GetCurrentBounds());
        }

        [Test]
        public void TryGetWarpDestination_ReturnsPartner()
        {
            var a = new GridPos(2, 2);
            var b = new GridPos(7, 7);
            SetWarpPair(a, b, 1);

            var dest = _warpService.TryGetWarpDestination(a);
            Assert.IsTrue(dest.HasValue);
            Assert.AreEqual(b, dest.Value);

            dest = _warpService.TryGetWarpDestination(b);
            Assert.IsTrue(dest.HasValue);
            Assert.AreEqual(a, dest.Value);
        }

        [Test]
        public void TryGetWarpDestination_NullForNonWarpTile()
        {
            var dest = _warpService.TryGetWarpDestination(new GridPos(5, 5));
            Assert.IsFalse(dest.HasValue);
        }

        [Test]
        public void TryGetWarpDestination_NullWhenPairCollapsed()
        {
            var a = new GridPos(2, 2);
            var b = new GridPos(7, 7);
            SetWarpPair(a, b, 1);

            // ペア先を崩落させる
            _model.SetTileCondition(b, TileCondition.Collapsed);

            var dest = _warpService.TryGetWarpDestination(a);
            Assert.IsFalse(dest.HasValue);
        }

        [Test]
        public void WarpTile_RecoverToWarpAfterCollapse()
        {
            var a = new GridPos(2, 2);
            var b = new GridPos(7, 7);
            SetWarpPair(a, b, 1);

            var timerService = new TileTimerService(_model);

            // ワープタイルを崩落
            _model.SetTileCondition(a, TileCondition.Collapsing);
            timerService.StartCollapseTimer(a, 1f, 2f);

            timerService.Tick(1.1f); // → Collapsed
            timerService.Tick(2.1f); // → Intact

            // Type は Warp のまま
            Assert.AreEqual(TileType.Warp, _model.GetTileType(a));
            Assert.AreEqual(TileCondition.Intact, _model.GetTileCondition(a));

            timerService.Dispose();
        }

        [Test]
        public void PairDestroyed_ConvertsOtherToNormal()
        {
            var a = new GridPos(0, 0);
            var b = new GridPos(5, 5);
            SetWarpPair(a, b, 1);

            // WarpService 経由で PD 通知
            _warpService.HandleTilePermanentlyDestroyed(a);
            _model.SetTileCondition(a, TileCondition.PermanentlyDestroyed);

            // b は Normal に変換されるはず
            Assert.AreEqual(TileType.Normal, _model.GetTileType(b));
        }

        [Test]
        public void TryGetWarpDestination_WorksWhenPairOnFire()
        {
            var a = new GridPos(2, 2);
            var b = new GridPos(7, 7);
            SetWarpPair(a, b, 1);

            // ペア先が炎上中
            _model.SetTileCondition(b, TileCondition.OnFire);

            // OnFire は hole ではないのでワープ可能
            var dest = _warpService.TryGetWarpDestination(a);
            Assert.IsTrue(dest.HasValue);
            Assert.AreEqual(b, dest.Value);
        }
    }
}
