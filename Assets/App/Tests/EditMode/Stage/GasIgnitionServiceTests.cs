using NUnit.Framework;
using FloorBreaker.Shared.Domain.Grid;
using FloorBreaker.Stage.Domain;

namespace FloorBreaker.Tests.EditMode.Stage
{
    [TestFixture]
    public class GasIgnitionServiceTests
    {
        private StageModel _model;
        private TileTimerService _timerService;
        private GasIgnitionService _gasService;

        [SetUp]
        public void SetUp()
        {
            _model = new StageModel(TileCoordRange.FromSize(10));
            _timerService = new TileTimerService(_model);
            _gasService = new GasIgnitionService(_model, _timerService, 0.1f, 3.5f);
        }

        [TearDown]
        public void TearDown()
        {
            _timerService.Dispose();
            _model.Dispose();
        }

        private void SetGas(GridPos pos)
        {
            _model.SetTileData(pos, new TileData { Type = TileType.Gas, Condition = TileCondition.Intact, WarpPairId = -1 });
        }

        [Test]
        public void Gas_IsPassable()
        {
            SetGas(new GridPos(5, 5));
            Assert.IsTrue(_model.IsPassable(new GridPos(5, 5)));
        }

        [Test]
        public void Ignite_SchedulesAdjacentGas()
        {
            // 3つのガスを一列に配置: (5,5) → (6,5) → (7,5)
            SetGas(new GridPos(5, 5));
            SetGas(new GridPos(6, 5));
            SetGas(new GridPos(7, 5));

            // (5,5) に火を付ける（これは BombEffectSpreadService が行う）
            _model.SetTileCondition(new GridPos(5, 5), TileCondition.OnFire);

            // (5,5) から引火開始
            _gasService.Ignite(new GridPos(5, 5));
            Assert.IsTrue(_gasService.HasPending);

            // 0.1秒後: (6,5) が引火
            _gasService.Tick(0.11f);
            Assert.AreEqual(TileCondition.OnFire, _model.GetTileCondition(new GridPos(6, 5)));
            // (7,5) はまだ
            Assert.AreEqual(TileCondition.Intact, _model.GetTileCondition(new GridPos(7, 5)));

            // さらに 0.1秒後: (7,5) が引火
            _gasService.Tick(0.11f);
            Assert.AreEqual(TileCondition.OnFire, _model.GetTileCondition(new GridPos(7, 5)));
        }

        [Test]
        public void Ignite_StopsAtNonGasTile()
        {
            SetGas(new GridPos(5, 5));
            // (6,5) は Normal（ガスではない）
            SetGas(new GridPos(7, 5));

            _model.SetTileCondition(new GridPos(5, 5), TileCondition.OnFire);
            _gasService.Ignite(new GridPos(5, 5));

            // 十分時間を経過させても (7,5) には引火しない（間に Normal がある）
            _gasService.Tick(1f);
            Assert.AreEqual(TileCondition.Intact, _model.GetTileCondition(new GridPos(7, 5)));
        }

        [Test]
        public void BreakBomb_OnGas_DoesNotIgnite()
        {
            SetGas(new GridPos(5, 5));
            SetGas(new GridPos(6, 5));

            // BreakBomb は Gas を Collapsing にするが引火しない
            _model.SetTileCondition(new GridPos(5, 5), TileCondition.Collapsing);

            // GasIgnitionService は呼ばれない（BombEffectSpreadService が呼ばない）
            Assert.IsFalse(_gasService.HasPending);
            Assert.AreEqual(TileCondition.Intact, _model.GetTileCondition(new GridPos(6, 5)));
        }

        [Test]
        public void Gas_RecoverToGasAfterFire()
        {
            SetGas(new GridPos(5, 5));
            _model.SetTileCondition(new GridPos(5, 5), TileCondition.OnFire);
            _timerService.StartFireTimer(new GridPos(5, 5), 1f);

            // 火が消える
            _timerService.Tick(1.1f);

            // Type は Gas のまま、Condition は Intact に復帰
            Assert.AreEqual(TileType.Gas, _model.GetTileType(new GridPos(5, 5)));
            Assert.AreEqual(TileCondition.Intact, _model.GetTileCondition(new GridPos(5, 5)));
        }

        [Test]
        public void Gas_RecoverToGasAfterCollapse()
        {
            SetGas(new GridPos(5, 5));
            _model.SetTileCondition(new GridPos(5, 5), TileCondition.Collapsing);
            _timerService.StartCollapseTimer(new GridPos(5, 5), 1f, 2f);

            // 崩落完了 + 復帰
            _timerService.Tick(1.1f);
            Assert.AreEqual(TileCondition.Collapsed, _model.GetTileCondition(new GridPos(5, 5)));
            _timerService.Tick(2.1f);

            Assert.AreEqual(TileType.Gas, _model.GetTileType(new GridPos(5, 5)));
            Assert.AreEqual(TileCondition.Intact, _model.GetTileCondition(new GridPos(5, 5)));
        }
    }
}
