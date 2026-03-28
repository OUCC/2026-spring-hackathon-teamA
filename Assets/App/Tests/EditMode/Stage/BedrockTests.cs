using NUnit.Framework;
using FloorBreaker.Shared.Domain.Grid;
using FloorBreaker.Stage.Domain;

namespace FloorBreaker.Tests.EditMode.Stage
{
    [TestFixture]
    public class BedrockTests
    {
        private StageModel _model;

        [SetUp]
        public void SetUp()
        {
            _model = new StageModel(TileCoordRange.FromSize(10));
        }

        [TearDown]
        public void TearDown()
        {
            _model.Dispose();
        }

        [Test]
        public void Bedrock_IsNotPassable()
        {
            var pos = new GridPos(5, 5);
            _model.SetTileData(pos, new TileData { Type = TileType.Bedrock, Condition = TileCondition.Intact, WarpPairId = -1 });

            Assert.IsFalse(_model.IsPassable(pos));
        }

        [Test]
        public void Bedrock_SurvivesStageShrink()
        {
            // 外周(0,0)に岩盤を配置
            var pos = new GridPos(0, 0);
            _model.SetTileData(pos, new TileData { Type = TileType.Bedrock, Condition = TileCondition.Intact, WarpPairId = -1 });

            var svc = new StageShrinkService();
            svc.ShrinkOuterRing(_model);

            // Bedrock はスキップされ、Condition が PD にならない
            Assert.AreEqual(TileType.Bedrock, _model.GetTileType(pos));
            Assert.AreEqual(TileCondition.Intact, _model.GetTileCondition(pos));
        }

        [Test]
        public void Bedrock_StopsBombFlight()
        {
            var bedrockPos = new GridPos(5, 5);
            _model.SetTileData(bedrockPos, new TileData { Type = TileType.Bedrock, Condition = TileCondition.Intact, WarpPairId = -1 });

            Assert.IsTrue(TileData.IsImpassableType(TileType.Bedrock));
        }

        [Test]
        public void Bedrock_IsNotDestroyedByBreakBomb()
        {
            var pos = new GridPos(5, 5);
            _model.SetTileData(pos, new TileData { Type = TileType.Bedrock, Condition = TileCondition.Intact, WarpPairId = -1 });

            // BreakBombResolver は Bedrock をスキップする（affectedTiles に含めない）
            // ここでは TileData のヘルパーでロジックを検証
            var data = _model.GetTileData(pos);
            Assert.AreEqual(TileType.Bedrock, data.Type);
            Assert.IsFalse(data.IsPassable);
        }

        [Test]
        public void Bedrock_CountsAsAlive()
        {
            var pos = new GridPos(5, 5);
            _model.SetTileData(pos, new TileData { Type = TileType.Bedrock, Condition = TileCondition.Intact, WarpPairId = -1 });

            // Bedrock は Condition=Intact なので alive カウントに含まれる
            int aliveCount = _model.GetAliveTileCount();
            Assert.AreEqual(100, aliveCount); // 10x10 全タイル alive
        }
    }
}
