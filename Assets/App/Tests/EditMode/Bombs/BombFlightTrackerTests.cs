using System.Collections.Generic;
using NUnit.Framework;
using R3;
using FloorBreaker.Shared.Domain.Grid;
using FloorBreaker.Shared.Domain.Primitives;
using FloorBreaker.Shared.Application.Interfaces;
using FloorBreaker.Stage.Domain;
using FloorBreaker.Player.Domain;
using FloorBreaker.Player.Application;
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

            var safeTileSearch = new SafeTileSearchService();
            var damageService = new PlayerDamageService(1.5f, 1f, _stage, safeTileSearch);
            var queryService = new StageQueryService(_stage);
            var areaResolver = new BombAreaResolver(queryService);
            var landingResolver = new BombLandingResolver(_stage);
            var breakResolver = new BreakBombResolver(areaResolver);
            var fireResolver = new FireBombResolver(areaResolver);
            var balance = new TestBalanceParameters();

            var spreadService = new BombEffectSpreadService(
                _stage, _tileTimerService, damageService, safeTileSearch, _slimeRegistry);
            var launchUseCase = new BombLaunchUseCase(
                landingResolver, breakResolver, fireResolver,
                _stage, balance, spreadService);

            _tracker = new BombFlightTracker(
                launchUseCase, _p1Cooldown, _p2Cooldown,
                _stage, _slimeRegistry, balance);

            var stats1 = new PlayerStats(10, 1f, 3f);
            var build1 = new PlayerBuild(3, 1, 1, 2f, 3.5f, false, 0.5f, 3, 1, 2, 4f, 3f, 1f);
            _player1 = new PlayerModel(PlayerId.Player1, new GridPos(2, 2), stats1, build1);

            var stats2 = new PlayerStats(10, 1f, 3f);
            var build2 = new PlayerBuild(3, 1, 1, 2f, 3.5f, false, 0.5f, 3, 1, 2, 4f, 3f, 1f);
            _player2 = new PlayerModel(PlayerId.Player2, new GridPos(8, 8), stats2, build2);

            _players = new List<PlayerModel> { _player1, _player2 };
        }

        [TearDown]
        public void TearDown()
        {
            _tracker?.Dispose();
            _p1Cooldown.Dispose();
            _p2Cooldown.Dispose();
            _player1.Dispose();
            _player2.Dispose();
            _tileTimerService.Dispose();
            _stage.Dispose();
        }

        private BombSpec CreateBreakSpec()
        {
            return new BombSpec(
                BombType.Break, 3, 3, 1, 2, 4f,
                true, 0f, 3f, 5f);
        }

        private BombSpec CreateFireSpec()
        {
            return new BombSpec(
                BombType.Fire, 3, 3, 1, 1, 2f,
                false, 3.5f, 0f, 0f);
        }

        [Test]
        public void StartFlight_WhenNotFlying_ReturnsTrue()
        {
            var result = _tracker.StartFlight(
                PlayerId.Player1, new GridPos(2, 2), Direction8.E, CreateBreakSpec());
            Assert.IsTrue(result);
            Assert.IsTrue(_tracker.IsFlying(PlayerId.Player1));
        }

        [Test]
        public void StartFlight_WhenAlreadyFlying_ReturnsFalse()
        {
            _tracker.StartFlight(
                PlayerId.Player1, new GridPos(2, 2), Direction8.E, CreateBreakSpec());

            var result = _tracker.StartFlight(
                PlayerId.Player1, new GridPos(2, 2), Direction8.E, CreateFireSpec());
            Assert.IsFalse(result);
        }

        [Test]
        public void StartFlight_WhenOnCooldown_ReturnsFalse()
        {
            // Start and land a bomb to trigger cooldown
            _tracker.StartFlight(
                PlayerId.Player1, new GridPos(2, 2), Direction8.E, CreateBreakSpec());
            _tracker.ReleaseBomb(PlayerId.Player1, _players);

            // Now try to start another break bomb flight (on cooldown)
            var result = _tracker.StartFlight(
                PlayerId.Player1, new GridPos(2, 2), Direction8.E, CreateBreakSpec());
            Assert.IsFalse(result);
        }

        [Test]
        public void Tick_AdvancesTileDistance()
        {
            _tracker.StartFlight(
                PlayerId.Player1, new GridPos(2, 2), Direction8.E, CreateBreakSpec());

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
                PlayerId.Player1, new GridPos(2, 2), Direction8.E, CreateBreakSpec());

            // Tick enough for bomb to reach the wall (2 tiles at speed 12 -> ~0.17s)
            _tracker.Tick(0.5f, _players);

            Assert.IsFalse(_tracker.IsFlying(PlayerId.Player1));
        }

        [Test]
        public void ReleaseBomb_LandsWhenMinDistanceReached()
        {
            // Min=3, Max=3: リリース → min まで飛行 → 自動着弾
            _tracker.StartFlight(
                PlayerId.Player1, new GridPos(2, 2), Direction8.E, CreateBreakSpec());

            _tracker.Tick(0.05f, _players);
            _tracker.ReleaseBomb(PlayerId.Player1, _players);

            // MinFlightDistance=3 未達なので飛行継続
            Assert.IsTrue(_tracker.IsFlying(PlayerId.Player1));

            // 十分 Tick → MinFlightDistance=3 に達して着弾
            _tracker.Tick(0.5f, _players);
            Assert.IsFalse(_tracker.IsFlying(PlayerId.Player1));
        }

        [Test]
        public void Tick_MaxDistance_AutoLands()
        {
            _tracker.StartFlight(
                PlayerId.Player1, new GridPos(2, 2), Direction8.E, CreateBreakSpec());

            // MaxFlightDistance = 3, speed = 12. Need 3/12 = 0.25s to cover 3 tiles.
            _tracker.Tick(0.5f, _players);

            Assert.IsFalse(_tracker.IsFlying(PlayerId.Player1));
        }

        [Test]
        public void StartFlight_EmitsFlightStartedEvent()
        {
            BombFlightStartedEvent? received = null;
            _tracker.FlightStarted.Subscribe(evt => received = evt);

            var origin = new GridPos(2, 2);
            var spec = CreateBreakSpec();
            _tracker.StartFlight(PlayerId.Player1, origin, Direction8.E, spec);

            Assert.IsNotNull(received);
            Assert.AreEqual(PlayerId.Player1, received.Value.Owner);
            Assert.AreEqual(origin, received.Value.Origin);
            Assert.AreEqual(Direction8.E, received.Value.Direction);
            Assert.AreEqual(BombType.Break, received.Value.Spec.Type);
        }

        [Test]
        public void ReleaseBomb_EmitsBombLandedEvent()
        {
            BombLandedEvent? received = null;
            _tracker.BombLanded.Subscribe(evt => received = evt);

            // Min=3, Max=3: リリース → Tick で min に到達 → 着弾イベント
            _tracker.StartFlight(
                PlayerId.Player1, new GridPos(2, 2), Direction8.E, CreateFireSpec());

            _tracker.Tick(0.05f, _players);
            _tracker.ReleaseBomb(PlayerId.Player1, _players);

            // min 未達なので Tick で飛行継続 → min 到達で着弾
            _tracker.Tick(0.5f, _players);

            Assert.IsNotNull(received);
            Assert.AreEqual(PlayerId.Player1, received.Value.Owner);
            Assert.AreEqual(BombType.Fire, received.Value.Type);
        }

        [Test]
        public void Tick_MaxDistance_EmitsBombLandedEvent()
        {
            BombLandedEvent? received = null;
            _tracker.BombLanded.Subscribe(evt => received = evt);

            var origin = new GridPos(2, 2);
            _tracker.StartFlight(
                PlayerId.Player1, origin, Direction8.E, CreateBreakSpec());

            // MaxFlightDistance=3, speed=12 → 0.25s. Tick 0.5s to ensure landing.
            _tracker.Tick(0.5f, _players);

            Assert.IsNotNull(received);
            Assert.AreEqual(PlayerId.Player1, received.Value.Owner);
            // Landing at origin + E*3 = (5, 2)
            Assert.AreEqual(new GridPos(5, 2), received.Value.LandingPos);
            Assert.AreEqual(BombType.Break, received.Value.Type);
        }

        // maxFlightDistance=10, minFlightDistance=3 の spec (最小飛行距離テスト用)
        private BombSpec CreateLongRangeBreakSpec()
        {
            return new BombSpec(
                BombType.Break, 10, 3, 1, 2, 4f,
                true, 0f, 3f, 5f);
        }

        [Test]
        public void ReleaseBomb_BeforeMinDistance_ContinuesFlying()
        {
            _tracker.StartFlight(
                PlayerId.Player1, new GridPos(2, 2), Direction8.E, CreateLongRangeBreakSpec());

            // Tick 少しだけ (1マス未満)
            _tracker.Tick(0.05f, _players);
            _tracker.ReleaseBomb(PlayerId.Player1, _players);

            // MinFlightDistance=3 に未達なので飛行継続
            Assert.IsTrue(_tracker.IsFlying(PlayerId.Player1));
        }

        [Test]
        public void ReleaseBomb_BeforeMinDistance_LandsAtMinDistance()
        {
            BombLandedEvent? received = null;
            _tracker.BombLanded.Subscribe(evt => received = evt);

            _tracker.StartFlight(
                PlayerId.Player1, new GridPos(2, 2), Direction8.E, CreateLongRangeBreakSpec());

            // 少し進めてからリリース
            _tracker.Tick(0.05f, _players);
            _tracker.ReleaseBomb(PlayerId.Player1, _players);

            // MinFlightDistance=3 に達するまで Tick
            _tracker.Tick(0.5f, _players);

            Assert.IsFalse(_tracker.IsFlying(PlayerId.Player1));
            Assert.IsNotNull(received);
            // origin(2,2) + E*3 = (5,2)
            Assert.AreEqual(new GridPos(5, 2), received.Value.LandingPos);
        }

        [Test]
        public void ReleaseBomb_AfterMinDistance_LandsImmediately()
        {
            _tracker.StartFlight(
                PlayerId.Player1, new GridPos(2, 2), Direction8.E, CreateLongRangeBreakSpec());

            // MinFlightDistance=3 を超えるまで Tick (3/12 = 0.25s)
            _tracker.Tick(0.4f, _players);

            Assert.IsTrue(_tracker.IsFlying(PlayerId.Player1));

            _tracker.ReleaseBomb(PlayerId.Player1, _players);

            // MinFlightDistance 超過済みなので即着弾
            Assert.IsFalse(_tracker.IsFlying(PlayerId.Player1));
        }

        private sealed class TestBalanceParameters : IBalanceParameters
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
            public bool BreakBombDefaultWallPenetration => true;
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
            public int BombMinFlightDistance => 3;
            public float StageShrinkAnimDuration => 1f;
            public float FireBombSpreadInterval => 0.15f;
            public float BreakBombSpreadInterval => 0.3f;
            public int HpRecoveryAmount => 3;
            public int HpRecoveryThreshold => 5;
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
