using NUnit.Framework;
using FloorBreaker.Shared.Domain.Grid;
using FloorBreaker.Stage.Domain;

namespace FloorBreaker.Tests.EditMode.Stage
{
    [TestFixture]
    public class EternalFireTests
    {
        private StageModel _model;
        private TileTimerService _timerService;

        [SetUp]
        public void SetUp()
        {
            _model = new StageModel(TileCoordRange.FromSize(10));
            _timerService = new TileTimerService(_model);
        }

        [TearDown]
        public void TearDown()
        {
            _timerService.Dispose();
            _model.Dispose();
        }

        [Test]
        public void EternalFire_IsPassable()
        {
            var pos = new GridPos(5, 5);
            _model.SetTileCondition(pos, TileCondition.EternalFire);

            Assert.IsTrue(_model.IsPassable(pos));
        }

        [Test]
        public void EternalFire_IsBurning()
        {
            Assert.IsTrue(TileData.IsBurning(TileCondition.EternalFire));
            Assert.IsTrue(TileData.IsBurning(TileCondition.OnFire));
            Assert.IsFalse(TileData.IsBurning(TileCondition.Intact));
        }

        [Test]
        public void EternalFire_DoesNotExpireWithFireTimer()
        {
            var pos = new GridPos(5, 5);
            _model.SetTileCondition(pos, TileCondition.EternalFire);

            // 炎タイマーを開始してみるが、EternalFire は別のタイマーシステムで管理される想定
            // ここでは EternalFire 状態が勝手に変わらないことを確認
            _timerService.Tick(10f); // 大量の時間を経過させても

            Assert.AreEqual(TileCondition.EternalFire, _model.GetTileCondition(pos));
        }

        [Test]
        public void EternalFire_CanBeCollapsedByBreakBomb()
        {
            var pos = new GridPos(5, 5);
            _model.SetTileCondition(pos, TileCondition.EternalFire);

            // BreakBomb の効果で Collapsing に変更される想定
            _model.SetTileCondition(pos, TileCondition.Collapsing);
            _timerService.StartCollapseTimer(pos, 1f, 2f);

            Assert.AreEqual(TileCondition.Collapsing, _model.GetTileCondition(pos));

            // 崩落完了
            _timerService.Tick(1.1f);
            Assert.AreEqual(TileCondition.Collapsed, _model.GetTileCondition(pos));

            // 復帰後は Normal+Intact（EternalFire は消火される）
            _timerService.Tick(2.1f);
            Assert.AreEqual(TileCondition.Intact, _model.GetTileCondition(pos));
            Assert.AreEqual(TileType.Normal, _model.GetTileType(pos));
        }

        [Test]
        public void EternalFire_PermanentlyDestroyedByStageShrink()
        {
            var pos = new GridPos(0, 0);
            _model.SetTileCondition(pos, TileCondition.EternalFire);

            var svc = new StageShrinkService();
            svc.ShrinkOuterRing(_model);

            Assert.AreEqual(TileCondition.PermanentlyDestroyed, _model.GetTileCondition(pos));
        }
    }
}
