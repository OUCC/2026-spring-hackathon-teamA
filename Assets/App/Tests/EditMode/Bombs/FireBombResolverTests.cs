using System.Linq;
using NUnit.Framework;
using FloorBreaker.Shared.Domain.Grid;
using FloorBreaker.Shared.Domain.Primitives;
using FloorBreaker.Stage.Domain;
using FloorBreaker.Bombs.Domain;

namespace FloorBreaker.Tests.EditMode.Bombs
{
    [TestFixture]
    public class FireBombResolverTests
    {
        private StageModel _stage;
        private FireBombResolver _resolver;

        [SetUp]
        public void SetUp()
        {
            _stage = new StageModel(TileCoordRange.FromSize(10));
            var queryService = new StageQueryService(_stage);
            var areaResolver = new BombAreaResolver(queryService);
            _resolver = new FireBombResolver(areaResolver);
        }

        [TearDown]
        public void TearDown()
        {
            _stage.Dispose();
        }

        private BombSpec MakeFireSpec(int range = 1, int damage = 1, float duration = 3.5f, bool wallPen = false)
        {
            return new BombSpec(BombType.Fire, 3, range, damage, 2f, false, wallPen, duration, 0f, 0f);
        }

        [Test]
        public void Resolve_Range1_Returns5AffectedTiles()
        {
            var result = _resolver.Resolve(new GridPos(5, 5), MakeFireSpec(), _stage);

            Assert.AreEqual(5, result.AffectedTiles.Count);
            Assert.AreEqual(0, result.WallsDestroyed.Count);
            Assert.AreEqual(1, result.ContactDamage);
            Assert.AreEqual(3.5f, result.FireDuration);
        }

        [Test]
        public void Resolve_NoPenetration_StopsAtWall()
        {
            _stage.SetTileState(new GridPos(6, 5), TileState.Wall);
            var result = _resolver.Resolve(new GridPos(5, 5), MakeFireSpec(range: 3), _stage);

            Assert.Contains(new GridPos(6, 5), (System.Collections.ICollection)result.AffectedTiles);
            Assert.IsFalse(result.AffectedTiles.Contains(new GridPos(7, 5)));
        }

        [Test]
        public void Resolve_WithPenetration_GoesThrough()
        {
            _stage.SetTileState(new GridPos(6, 5), TileState.Wall);
            var result = _resolver.Resolve(new GridPos(5, 5), MakeFireSpec(range: 3, wallPen: true), _stage);

            Assert.Contains(new GridPos(6, 5), (System.Collections.ICollection)result.AffectedTiles);
            Assert.Contains(new GridPos(7, 5), (System.Collections.ICollection)result.AffectedTiles);
        }

        [Test]
        public void Resolve_WallInRange_IsDestroyed()
        {
            _stage.SetTileState(new GridPos(6, 5), TileState.Wall);
            var result = _resolver.Resolve(new GridPos(5, 5), MakeFireSpec(), _stage);

            Assert.Contains(new GridPos(6, 5), (System.Collections.ICollection)result.WallsDestroyed);
        }

        [Test]
        public void Resolve_SkipsCollapsedTiles()
        {
            _stage.SetTileState(new GridPos(6, 5), TileState.Collapsed);
            var result = _resolver.Resolve(new GridPos(5, 5), MakeFireSpec(), _stage);

            Assert.IsFalse(result.AffectedTiles.Contains(new GridPos(6, 5)));
        }

        [Test]
        public void Resolve_SkipsCollapsingTiles()
        {
            _stage.SetTileState(new GridPos(6, 5), TileState.Collapsing);
            var result = _resolver.Resolve(new GridPos(5, 5), MakeFireSpec(), _stage);

            Assert.IsFalse(result.AffectedTiles.Contains(new GridPos(6, 5)));
        }

        [Test]
        public void Resolve_ReFiresOnFireTile()
        {
            _stage.SetTileState(new GridPos(6, 5), TileState.OnFire);
            var result = _resolver.Resolve(new GridPos(5, 5), MakeFireSpec(), _stage);

            Assert.Contains(new GridPos(6, 5), (System.Collections.ICollection)result.AffectedTiles);
        }
    }
}
