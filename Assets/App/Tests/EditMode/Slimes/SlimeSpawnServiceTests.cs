using System.Collections.Generic;
using NUnit.Framework;
using FloorBreaker.Shared.Domain.Grid;
using FloorBreaker.Shared.Domain.Primitives;
using FloorBreaker.Shared.Application.Interfaces;
using FloorBreaker.Shared.Infrastructure.Random;
using FloorBreaker.Player.Domain;
using FloorBreaker.Stage.Domain;
using FloorBreaker.Slimes.Domain;

namespace FloorBreaker.Tests.EditMode.Slimes
{
    [TestFixture]
    public class SlimeSpawnServiceTests
    {
        private StageModel _stage;
        private SlimeRegistry _registry;
        private SlimeSpawnService _svc;
        private List<PlayerModel> _players;
        private IRandomProvider _random;

        [SetUp]
        public void SetUp()
        {
            // 30x30 stage => 900 alive tiles
            _stage = new StageModel(TileCoordRange.FromSize(30));
            _registry = new SlimeRegistry();

            var stats1 = new PlayerStats(10, 1f, 3f);
            var build1 = new PlayerBuild(3, 1, 1, 2f, 3.5f, false, 0.5f, 3, 1, 2, 4f, 3f, 1f);
            var player1 = new PlayerModel(PlayerId.Player1, new GridPos(15, 15), stats1, build1);

            var stats2 = new PlayerStats(10, 1f, 3f);
            var build2 = new PlayerBuild(3, 1, 1, 2f, 3.5f, false, 0.5f, 3, 1, 2, 4f, 3f, 1f);
            var player2 = new PlayerModel(PlayerId.Player2, new GridPos(15, 15), stats2, build2);

            _players = new List<PlayerModel> { player1, player2 };
            _random = new SeededRandomProvider(42);
            var balance = new TestBalanceParameters();
            _svc = new SlimeSpawnService(_stage, _registry, _players, _random, balance);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var p in _players) p.Dispose();
            _stage.Dispose();
        }

        [Test]
        public void SpawnIfNeeded_SpawnsToTarget()
        {
            // 900 tiles * 0.03 = 27 target, registry empty => spawn 27
            var spawned = _svc.SpawnIfNeeded();
            Assert.AreEqual(27, spawned.Count);
            Assert.AreEqual(27, _registry.AliveCount);
        }

        [Test]
        public void SpawnIfNeeded_RespectsMinDistance()
        {
            var balance = new TestBalanceParameters();
            var spawned = _svc.SpawnIfNeeded();

            foreach (var slime in spawned)
            {
                foreach (var player in _players)
                {
                    int dist = slime.Position.ChebyshevDistance(player.CurrentPosition);
                    Assert.GreaterOrEqual(dist, balance.SlimeMinDistanceFromPlayer,
                        $"Slime at {slime.Position} too close to player at {player.CurrentPosition}");
                }
            }
        }

        [Test]
        public void SpawnIfNeeded_NoSpawnWhenAtTarget()
        {
            // First spawn fills to target
            _svc.SpawnIfNeeded();
            int countAfterFirst = _registry.AliveCount;

            // Second spawn should add nothing
            var spawned2 = _svc.SpawnIfNeeded();
            Assert.AreEqual(0, spawned2.Count);
            Assert.AreEqual(countAfterFirst, _registry.AliveCount);
        }

        private class TestBalanceParameters : IBalanceParameters
        {
            public int InitialHp => 10;
            public float BaseMovementSpeed => 1f;
            public float MaxMovementSpeed => 2f;
            public float MovementSpeedIncrement => 0.2f;
            public int BreakBombMaxFlightDistance => 3;
            public int BreakBombEffectRange => 1;
            public int BreakBombDamage => 2;
            public float BreakBombCollapseDuration => 3f;
            public float BreakBombRecoveryDuration => 5f;
            public float BreakBombCooldown => 4f;
            public float BreakBombCooldownMin => 1f;
            public float BreakBombCooldownReduction => 0.5f;
            public bool BreakBombDefaultWallPenetration => false;
            public int FireBombMaxFlightDistance => 3;
            public int FireBombEffectRange => 1;
            public int FireBombContactDamage => 1;
            public int FireBombDotDamage => 1;
            public float FireBombDotInterval => 1f;
            public float FireBombDuration => 3.5f;
            public float FireBombCooldown => 2f;
            public float FireBombCooldownMin => 0.5f;
            public float FireBombCooldownReduction => 0.3f;
            public bool FireBombDefaultWallPenetration => false;
            public int StageSize => 30;
            public float WallSeedPercent => 0.08f;
            public float WallGrowthChance => 0.4f;
            public float WallTargetPercent => 0.15f;
            public int SpawnProtectionRadius => 2;
            public float SlimeSpawnCheckInterval => 5f;
            public float SlimeTargetRatio => 0.03f;
            public int SlimeMinDistanceFromPlayer => 5;
            public int SlimeHp => 1;
            public float SlimeSpeedMultiplier => 0.5f;
            public int SlimeDetectionRange => 5;
            public int SlimeAttackDamage => 1;
            public float SlimeAttackCooldown => 1f;
            public int SlimeSpawnRatioNormal => 10;
            public int SlimeSpawnRatioGold => 1;
            public int SlimeSpawnRatioRed => 1;
            public float PhaseDuration => 20f;
            public float UpgradeSelectionTimeout => 10f;
            public int UpgradeChoiceCount => 3;
            public int RerollCost => 1;
            public float ForcedMoveDuration => 1f;
            public int HpRecoveryAmount => 3;
            public int HpRecoveryThreshold => 5;
            public float InvulnerabilityDuration => 1.5f;
            public float BombFlightSpeed => 12f;
            public int BombMinFlightDistance => 3;
            public float StageShrinkAnimDuration => 1f;
            public float FireBombSpreadInterval => 0.15f;
            public float BreakBombSpreadInterval => 0.3f;
            public float DashCooldown => 1f;
            public float DashDoubleTapWindow => 0.3f;
            public int FireFlightRangeIncrement => 2;
            public int FireEffectRangeIncrement => 1;
            public int FireDamageIncrement => 1;
            public float FireDurationIncrement => 2f;
            public float FireCooldownReduction => 0.3f;
            public int BreakFlightRangeIncrement => 2;
            public int BreakEffectRangeIncrement => 1;
            public int BreakDamageIncrement => 1;
            public float BreakCollapseTimeIncrement => 2f;
            public float BreakCooldownReduction => 0.5f;
        }
    }
}
