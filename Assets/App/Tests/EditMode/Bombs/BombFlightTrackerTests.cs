using System.Collections.Generic;
using NUnit.Framework;
using FloorBreaker.Shared.Domain.Grid;
using FloorBreaker.Shared.Domain.Primitives;
using FloorBreaker.Shared.Application.Interfaces;
using FloorBreaker.Stage.Domain;
using FloorBreaker.Player.Domain;
using FloorBreaker.Bombs.Domain;
using FloorBreaker.Bombs.Application;
using FloorBreaker.Slimes.Domain;

namespace FloorBreaker.Tests.EditMode.Bombs
{
    [TestFixture]
    public class BombFlightTrackerTests
    {
        private StageModel _stage;
        private TileTimerService _tileTimerService;
        private BombCooldownState _p1Cooldown;
        private BombCooldownState _p2Cooldown;
        private SlimeRegistry _slimeRegistry;
        private BombFlightTracker _tracker;
        private PlayerModel _player1;
        private PlayerModel _player2;
        private List<PlayerModel> _players;

        [SetUp]
        public void SetUp()
        {
            _stage = new StageModel(TileCoordRange.FromSize(10));
            _tileTimerService = new TileTimerService(_stage);
            _p1Cooldown = new BombCooldownState();
            _p2Cooldown = new BombCooldownState();
            _slimeRegistry = new SlimeRegistry();

            var damageService = new PlayerDamageService(1.5f, 1f);
            var safeTileSearch = new SafeTileSearchService();
            var queryService = new StageQueryService(_stage);
            var areaResolver = new BombAreaResolver(queryService);
            var landingResolver = new BombLandingResolver(_stage);
            var fallResolver = new FallBombResolver(areaResolver);
            var fireResolver = new FireBombResolver(areaResolver);
            var balance = new TestBalanceParameters();

            var launchUseCase = new BombLaunchUseCase(
                landingResolver, fallResolver, fireResolver,
                _stage, _tileTimerService, damageService, safeTileSearch,
                balance, _slimeRegistry);

            _tracker = new BombFlightTracker(
                launchUseCase, _p1Cooldown, _p2Cooldown,
                _stage, _slimeRegistry, balance);

            var stats1 = new PlayerStats(10, 1f, 2f);
            var build1 = new PlayerBuild(3, 1, 1, 2f, 3.5f, false, 0.5f, 3, 1, 2, 4f, 3f, 1f);
            _player1 = new PlayerModel(PlayerId.Player1, new GridPos(2, 2), stats1, build1);

            var stats2 = new PlayerStats(10, 1f, 2f);
            var build2 = new PlayerBuild(3, 1, 1, 2f, 3.5f, false, 0.5f, 3, 1, 2, 4f, 3f, 1f);
            _player2 = new PlayerModel(PlayerId.Player2, new GridPos(8, 8), stats2, build2);

            _players = new List<PlayerModel> { _player1, _player2 };
        }

        [TearDown]
        public void TearDown()
        {
            _p1Cooldown.Dispose();
            _p2Cooldown.Dispose();
            _player1.Dispose();
            _player2.Dispose();
            _tileTimerService.Dispose();
            _stage.Dispose();
        }

        private BombSpec CreateFallSpec()
        {
            return new BombSpec(
                BombType.Fall, 3, 1, 2, 4f,
                false, true, 0f, 3f, 5f);
        }

        private BombSpec CreateFireSpec()
        {
            return new BombSpec(
                BombType.Fire, 3, 1, 1, 2f,
                false, false, 3.5f, 0f, 0f);
        }

        [Test]
        public void StartFlight_WhenNotFlying_ReturnsTrue()
        {
            var result = _tracker.StartFlight(
                PlayerId.Player1, new GridPos(2, 2), Direction8.E, CreateFallSpec());
            Assert.IsTrue(result);
            Assert.IsTrue(_tracker.IsFlying(PlayerId.Player1));
        }

        [Test]
        public void StartFlight_WhenAlreadyFlying_ReturnsFalse()
        {
            _tracker.StartFlight(
                PlayerId.Player1, new GridPos(2, 2), Direction8.E, CreateFallSpec());

            var result = _tracker.StartFlight(
                PlayerId.Player1, new GridPos(2, 2), Direction8.E, CreateFireSpec());
            Assert.IsFalse(result);
        }

        [Test]
        public void StartFlight_WhenOnCooldown_ReturnsFalse()
        {
            // Start and land a bomb to trigger cooldown
            _tracker.StartFlight(
                PlayerId.Player1, new GridPos(2, 2), Direction8.E, CreateFallSpec());
            _tracker.ReleaseBomb(PlayerId.Player1, _players);

            // Now try to start another fall bomb flight (on cooldown)
            var result = _tracker.StartFlight(
                PlayerId.Player1, new GridPos(2, 2), Direction8.E, CreateFallSpec());
            Assert.IsFalse(result);
        }

        [Test]
        public void Tick_AdvancesTileDistance()
        {
            _tracker.StartFlight(
                PlayerId.Player1, new GridPos(2, 2), Direction8.E, CreateFallSpec());

            // BombFlightSpeed = 12, so 1 tile in ~0.084s. Tick a small amount.
            // After 0.05s: accumulator = 0.6 tiles, still less than 1
            _tracker.Tick(0.05f, _players);
            Assert.IsTrue(_tracker.IsFlying(PlayerId.Player1));
        }

        [Test]
        public void Tick_WallCollision_AutoLands()
        {
            // Place a wall 2 tiles east of origin
            _stage.SetTileState(new GridPos(4, 2), TileState.Wall);

            _tracker.StartFlight(
                PlayerId.Player1, new GridPos(2, 2), Direction8.E, CreateFallSpec());

            // Tick enough for bomb to reach the wall (2 tiles at speed 12 -> ~0.17s)
            _tracker.Tick(0.5f, _players);

            Assert.IsFalse(_tracker.IsFlying(PlayerId.Player1));
        }

        [Test]
        public void ReleaseBomb_LandsAtCurrentPosition()
        {
            _tracker.StartFlight(
                PlayerId.Player1, new GridPos(2, 2), Direction8.E, CreateFallSpec());

            // Tick a small amount so bomb has moved slightly
            _tracker.Tick(0.05f, _players);

            _tracker.ReleaseBomb(PlayerId.Player1, _players);
            Assert.IsFalse(_tracker.IsFlying(PlayerId.Player1));
        }

        [Test]
        public void Tick_MaxDistance_AutoLands()
        {
            _tracker.StartFlight(
                PlayerId.Player1, new GridPos(2, 2), Direction8.E, CreateFallSpec());

            // MaxFlightDistance = 3, speed = 12. Need 3/12 = 0.25s to cover 3 tiles.
            _tracker.Tick(0.5f, _players);

            Assert.IsFalse(_tracker.IsFlying(PlayerId.Player1));
        }

        private sealed class TestBalanceParameters : IBalanceParameters
        {
            public int InitialHp => 10;
            public float BaseMovementSpeed => 1f;
            public float MaxMovementSpeed => 2f;
            public float MovementSpeedIncrement => 0.2f;
            public int FallBombMaxFlightDistance => 3;
            public int FallBombEffectRange => 1;
            public int FallBombDamage => 2;
            public float FallBombCollapseDuration => 3f;
            public float FallBombRecoveryDuration => 5f;
            public float FallBombCooldown => 4f;
            public float FallBombCooldownMin => 1f;
            public float FallBombCooldownReduction => 0.5f;
            public bool FallBombDefaultWallPenetration => true;
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
            public float WallTargetPercent => 0.2f;
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
            public float InvulnerabilityDuration => 1.5f;
            public float BombFlightSpeed => 12f;
            public float StageShrinkAnimDuration => 1f;
            public int HpRecoveryAmount => 3;
            public int HpRecoveryThreshold => 5;
        }
    }
}
