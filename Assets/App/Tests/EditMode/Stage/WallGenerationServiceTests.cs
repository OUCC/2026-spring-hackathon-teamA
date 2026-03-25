using NUnit.Framework;
using FloorBreaker.Shared.Domain.Grid;
using FloorBreaker.Shared.Infrastructure.Random;
using FloorBreaker.Stage.Domain;

namespace FloorBreaker.Tests.EditMode.Stage
{
    [TestFixture]
    public class WallGenerationServiceTests
    {
        private static readonly TileCoordRange Bounds30 = TileCoordRange.FromSize(30);
        private static readonly GridPos P1Spawn = new(2, 2);
        private static readonly GridPos P2Spawn = new(27, 27);

        [Test]
        public void WallCoverage_IsApproximately20Percent()
        {
            var svc = new WallGenerationService(0.08f, 0.4f, 0.20f, 2);
            var rng = new SeededRandomProvider(42);
            var walls = svc.Generate(Bounds30, P1Spawn, P2Spawn, rng);

            float ratio = (float)walls.Count / Bounds30.TileCount;
            Assert.GreaterOrEqual(ratio, 0.10f, $"Wall ratio too low: {ratio:P1}");
            Assert.LessOrEqual(ratio, 0.30f, $"Wall ratio too high: {ratio:P1}");
        }

        [Test]
        public void SpawnZones_AreClear()
        {
            var svc = new WallGenerationService(0.08f, 0.4f, 0.20f, 2);
            var rng = new SeededRandomProvider(42);
            var walls = svc.Generate(Bounds30, P1Spawn, P2Spawn, rng);

            // 5x5 around each spawn should be clear
            for (int dx = -2; dx <= 2; dx++)
            {
                for (int dy = -2; dy <= 2; dy++)
                {
                    var p1Zone = new GridPos(P1Spawn.X + dx, P1Spawn.Y + dy);
                    var p2Zone = new GridPos(P2Spawn.X + dx, P2Spawn.Y + dy);
                    Assert.IsFalse(walls.Contains(p1Zone), $"Wall in P1 spawn zone at {p1Zone}");
                    Assert.IsFalse(walls.Contains(p2Zone), $"Wall in P2 spawn zone at {p2Zone}");
                }
            }
        }

        [Test]
        public void SameSeed_ProducesSameResult()
        {
            var svc = new WallGenerationService(0.08f, 0.4f, 0.20f, 2);
            var walls1 = svc.Generate(Bounds30, P1Spawn, P2Spawn, new SeededRandomProvider(99));
            var walls2 = svc.Generate(Bounds30, P1Spawn, P2Spawn, new SeededRandomProvider(99));

            Assert.AreEqual(walls1.Count, walls2.Count);
            Assert.IsTrue(walls1.SetEquals(walls2));
        }

        [Test]
        public void ZeroTargetPercent_ProducesNoWalls()
        {
            var svc = new WallGenerationService(0f, 0f, 0f, 2);
            var rng = new SeededRandomProvider(42);
            var walls = svc.Generate(Bounds30, P1Spawn, P2Spawn, rng);

            Assert.AreEqual(0, walls.Count);
        }
    }
}
