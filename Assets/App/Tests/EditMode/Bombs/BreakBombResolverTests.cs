using System.Linq;
using NUnit.Framework;
using FloorBreaker.Shared.Domain.Grid;
using FloorBreaker.Shared.Domain.Primitives;
using FloorBreaker.Stage.Domain;
using FloorBreaker.Bombs.Domain;

namespace FloorBreaker.Tests.EditMode.Bombs
{
    [TestFixture]
    public class BreakBombResolverTests
    {
        private StageModel _stage;
        private BreakBombResolver _resolver;

        [SetUp]
        public void SetUp()
        {
            _stage = new StageModel(TileCoordRange.FromSize(10));
            var queryService = new StageQueryService(_stage);
            var areaResolver = new BombAreaResolver(queryService);
            _resolver = new BreakBombResolver(areaResolver);
        }

        [TearDown]
        public void TearDown()
        {
            _stage.Dispose();
        }

        private BombSpec MakeBreakSpec(int range = 1, int damage = 2, float collapse = 3f, float recovery = 5f)
        {
            return new BombSpec(BombType.Break, 3, 3, range, damage, 4f, true, 0f, collapse, recovery);
        }

        [Test]
        public void Resolve_Range1_Returns5AffectedTiles()
        {
            var result = _resolver.Resolve(new GridPos(5, 5), MakeBreakSpec(), _stage);

            Assert.AreEqual(5, result.AffectedTiles.Count);
            Assert.AreEqual(0, result.WallsDestroyed.Count);
            Assert.AreEqual(2, result.Damage);
            Assert.AreEqual(3f, result.CollapseTime);
            Assert.AreEqual(5f, result.RecoveryTime);
        }

        [Test]
        public void Resolve_WithWall_IncludesWallInDestroyed()
        {
            _stage.SetTileData(new GridPos(6, 5), new TileData { Type = TileType.Wall, Condition = TileCondition.Intact, WarpPairId = -1 });
            var result = _resolver.Resolve(new GridPos(5, 5), MakeBreakSpec(), _stage);

            Assert.Contains(new GridPos(6, 5), (System.Collections.ICollection)result.WallsDestroyed);
            Assert.Contains(new GridPos(6, 5), (System.Collections.ICollection)result.AffectedTiles);
        }

        [Test]
        public void Resolve_WallPenetration_IncludesTilesBeyondWall()
        {
            _stage.SetTileData(new GridPos(6, 5), new TileData { Type = TileType.Wall, Condition = TileCondition.Intact, WarpPairId = -1 });
            var spec = MakeBreakSpec(range: 2);
            var result = _resolver.Resolve(new GridPos(5, 5), spec, _stage);

            // 壁貫通=true なので壁の先も含む
            Assert.Contains(new GridPos(7, 5), (System.Collections.ICollection)result.AffectedTiles);
        }

        [Test]
        public void Resolve_IncludesCollapsedTiles()
        {
            _stage.SetTileCondition(new GridPos(6, 5), TileCondition.Collapsed);
            var result = _resolver.Resolve(new GridPos(5, 5), MakeBreakSpec(), _stage);

            Assert.IsTrue(result.AffectedTiles.Contains(new GridPos(6, 5)));
        }

        [Test]
        public void Resolve_SkipsPermanentlyDestroyedTiles()
        {
            _stage.SetTileCondition(new GridPos(6, 5), TileCondition.PermanentlyDestroyed);
            var result = _resolver.Resolve(new GridPos(5, 5), MakeBreakSpec(), _stage);

            Assert.IsFalse(result.AffectedTiles.Contains(new GridPos(6, 5)));
        }

        [Test]
        public void Resolve_IncludesCollapsingTiles()
        {
            _stage.SetTileCondition(new GridPos(6, 5), TileCondition.Collapsing);
            var result = _resolver.Resolve(new GridPos(5, 5), MakeBreakSpec(), _stage);

            Assert.IsTrue(result.AffectedTiles.Contains(new GridPos(6, 5)));
        }

        [Test]
        public void Resolve_IncludesOnFireTiles()
        {
            _stage.SetTileCondition(new GridPos(6, 5), TileCondition.OnFire);
            var result = _resolver.Resolve(new GridPos(5, 5), MakeBreakSpec(), _stage);

            Assert.Contains(new GridPos(6, 5), (System.Collections.ICollection)result.AffectedTiles);
        }
    }
}
