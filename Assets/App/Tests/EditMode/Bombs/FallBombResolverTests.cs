using System.Linq;
using NUnit.Framework;
using FloorBreaker.Shared.Domain.Grid;
using FloorBreaker.Shared.Domain.Primitives;
using FloorBreaker.Stage.Domain;
using FloorBreaker.Bombs.Domain;

namespace FloorBreaker.Tests.EditMode.Bombs
{
    [TestFixture]
    public class FallBombResolverTests
    {
        private StageModel _stage;
        private FallBombResolver _resolver;

        [SetUp]
        public void SetUp()
        {
            _stage = new StageModel(TileCoordRange.FromSize(10));
            var queryService = new StageQueryService(_stage);
            var areaResolver = new BombAreaResolver(queryService);
            _resolver = new FallBombResolver(areaResolver);
        }

        [TearDown]
        public void TearDown()
        {
            _stage.Dispose();
        }

        private BombSpec MakeFallSpec(int range = 1, int damage = 2, float collapse = 3f, float recovery = 5f)
        {
            return new BombSpec(BombType.Fall, 3, 3, range, damage, 4f, false, true, 0f, collapse, recovery);
        }

        [Test]
        public void Resolve_Range1_Returns5AffectedTiles()
        {
            var result = _resolver.Resolve(new GridPos(5, 5), MakeFallSpec(), _stage);

            Assert.AreEqual(5, result.AffectedTiles.Count);
            Assert.AreEqual(0, result.WallsDestroyed.Count);
            Assert.AreEqual(2, result.Damage);
            Assert.AreEqual(3f, result.CollapseTime);
            Assert.AreEqual(5f, result.RecoveryTime);
        }

        [Test]
        public void Resolve_WithWall_IncludesWallInDestroyed()
        {
            _stage.SetTileState(new GridPos(6, 5), TileState.Wall);
            var result = _resolver.Resolve(new GridPos(5, 5), MakeFallSpec(), _stage);

            Assert.Contains(new GridPos(6, 5), (System.Collections.ICollection)result.WallsDestroyed);
            Assert.Contains(new GridPos(6, 5), (System.Collections.ICollection)result.AffectedTiles);
        }

        [Test]
        public void Resolve_WallPenetration_IncludesTilesBeyondWall()
        {
            _stage.SetTileState(new GridPos(6, 5), TileState.Wall);
            var spec = MakeFallSpec(range: 2);
            var result = _resolver.Resolve(new GridPos(5, 5), spec, _stage);

            // 壁貫通=true なので壁の先も含む
            Assert.Contains(new GridPos(7, 5), (System.Collections.ICollection)result.AffectedTiles);
        }

        [Test]
        public void Resolve_IncludesCollapsedTiles()
        {
            _stage.SetTileState(new GridPos(6, 5), TileState.Collapsed);
            var result = _resolver.Resolve(new GridPos(5, 5), MakeFallSpec(), _stage);

            Assert.IsTrue(result.AffectedTiles.Contains(new GridPos(6, 5)));
        }

        [Test]
        public void Resolve_SkipsPermanentlyDestroyedTiles()
        {
            _stage.SetTileState(new GridPos(6, 5), TileState.PermanentlyDestroyed);
            var result = _resolver.Resolve(new GridPos(5, 5), MakeFallSpec(), _stage);

            Assert.IsFalse(result.AffectedTiles.Contains(new GridPos(6, 5)));
        }

        [Test]
        public void Resolve_IncludesCollapsingTiles()
        {
            _stage.SetTileState(new GridPos(6, 5), TileState.Collapsing);
            var result = _resolver.Resolve(new GridPos(5, 5), MakeFallSpec(), _stage);

            Assert.IsTrue(result.AffectedTiles.Contains(new GridPos(6, 5)));
        }

        [Test]
        public void Resolve_IncludesOnFireTiles()
        {
            _stage.SetTileState(new GridPos(6, 5), TileState.OnFire);
            var result = _resolver.Resolve(new GridPos(5, 5), MakeFallSpec(), _stage);

            Assert.Contains(new GridPos(6, 5), (System.Collections.ICollection)result.AffectedTiles);
        }
    }
}
