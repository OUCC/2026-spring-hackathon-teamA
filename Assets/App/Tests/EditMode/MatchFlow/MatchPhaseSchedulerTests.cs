using System.Collections.Generic;
using NUnit.Framework;
using FloorBreaker.Shared.Domain.Grid;
using FloorBreaker.Shared.Domain.Primitives;
using FloorBreaker.Shared.Domain.Timing;
using FloorBreaker.Shared.Application.Interfaces;
using FloorBreaker.Shared.Infrastructure.Random;
using FloorBreaker.Stage.Domain;
using FloorBreaker.Player.Domain;
using FloorBreaker.Bombs.Domain;
using FloorBreaker.Bombs.Application;
using FloorBreaker.Slimes.Domain;
using FloorBreaker.Slimes.Application;
using FloorBreaker.Upgrades.Domain;
using FloorBreaker.MatchFlow.Application;

namespace FloorBreaker.Tests.EditMode.MatchFlow
{
    [TestFixture]
    public class MatchPhaseSchedulerTests
    {
        private StageModel _stage;
        private MatchClock _clock;
        private TileTimerService _tileTimerService;
        private BombCooldownState _p1Cooldown;
        private BombCooldownState _p2Cooldown;
        private SlimeRegistry _slimeRegistry;
        private SlimeTickService _slimeTickService;
        private FireDamageTickService _fireDamageTickService;
        private StageShrinkService _stageShrinkService;
        private UpgradePhaseUseCase _upgradePhaseUseCase;
        private MatchEndUseCase _matchEndUseCase;
        private PlayerDamageService _playerDamageService;
        private SafeTileSearchService _safeTileSearch;
        private PlayerModel _player1;
        private PlayerModel _player2;
        private List<PlayerModel> _players;
        private UpgradeDraftService _draftP1;
        private UpgradeDraftService _draftP2;
        private MatchPhaseScheduler _scheduler;
        private TestBalanceParameters _balance;

        [SetUp]
        public void SetUp()
        {
            _balance = new TestBalanceParameters();
            _stage = new StageModel(TileCoordRange.FromSize(10));
            _clock = new MatchClock(20f);
            _tileTimerService = new TileTimerService(_stage);
            _p1Cooldown = new BombCooldownState();
            _p2Cooldown = new BombCooldownState();
            _slimeRegistry = new SlimeRegistry();
            _playerDamageService = new PlayerDamageService(1.5f, 1f);
            _safeTileSearch = new SafeTileSearchService();

            var slimeAi = new SlimeAiService(_playerDamageService, _safeTileSearch);
            var slimeSpawn = new SlimeSpawnService();
            _slimeTickService = new SlimeTickService(slimeAi, slimeSpawn, _slimeRegistry, _tileTimerService);

            _fireDamageTickService = new FireDamageTickService(
                _playerDamageService, _safeTileSearch, _slimeRegistry, _balance);

            _stageShrinkService = new StageShrinkService();
            _matchEndUseCase = new MatchEndUseCase();

            var catalog = new UpgradeCatalog();
            var availabilityRule = new UpgradeAvailabilityRule(_balance);
            var rollRule = new UpgradeRollRule(catalog, availabilityRule);
            var applyService = new UpgradeApplyService(_balance);

            _draftP1 = new UpgradeDraftService(rollRule, applyService, _balance);
            _draftP2 = new UpgradeDraftService(rollRule, applyService, _balance);
            _upgradePhaseUseCase = new UpgradePhaseUseCase(_draftP1, _draftP2, _balance);

            var stats1 = new PlayerStats(10, 1f, 2f);
            var build1 = new PlayerBuild(3, 1, 1, 2f, 3.5f, false, 0.5f, 3, 1, 2, 4f, 3f, 1f);
            _player1 = new PlayerModel(PlayerId.Player1, new GridPos(2, 2), stats1, build1);

            var stats2 = new PlayerStats(10, 1f, 2f);
            var build2 = new PlayerBuild(3, 1, 1, 2f, 3.5f, false, 0.5f, 3, 1, 2, 4f, 3f, 1f);
            _player2 = new PlayerModel(PlayerId.Player2, new GridPos(7, 7), stats2, build2);

            _players = new List<PlayerModel> { _player1, _player2 };

            var random = new SeededRandomProvider(42);

            _scheduler = new MatchPhaseScheduler(
                _clock, _tileTimerService,
                _p1Cooldown, _p2Cooldown,
                _slimeTickService, _fireDamageTickService,
                null, // BombFlightTracker is optional
                null, // BombEffectSpreadService is optional
                _stageShrinkService, _upgradePhaseUseCase, _matchEndUseCase,
                _playerDamageService, _safeTileSearch,
                _players, _stage, _slimeRegistry,
                _balance, random);
        }

        [TearDown]
        public void TearDown()
        {
            _draftP1.Dispose();
            _draftP2.Dispose();
            _slimeTickService.Dispose();
            _p1Cooldown.Dispose();
            _p2Cooldown.Dispose();
            _player1.Dispose();
            _player2.Dispose();
            _tileTimerService.Dispose();
            _clock.Dispose();
            _stage.Dispose();
        }

        [Test]
        public void InitialState_IsRunning()
        {
            Assert.AreEqual(SchedulerState.Running, _scheduler.State);
            Assert.AreEqual(GamePhase.MatchRunning, _scheduler.Clock.CurrentPhaseValue);
        }

        [Test]
        public void Tick20Seconds_TransitionsToStageShrink()
        {
            _scheduler.Tick(20f);
            Assert.AreEqual(SchedulerState.StageShrink, _scheduler.State);
        }

        [Test]
        public void StageShrink_AfterAnimDuration_TransitionsToUpgradePhase()
        {
            // Running -> StageShrink
            _scheduler.Tick(20f);
            Assert.AreEqual(SchedulerState.StageShrink, _scheduler.State);

            // StageShrink -> UpgradePhase (after 1s anim duration)
            _scheduler.Tick(1f);
            Assert.AreEqual(SchedulerState.UpgradePhase, _scheduler.State);
        }

        [Test]
        public void UpgradePhase_BothSkip_TransitionsToRunning()
        {
            // Running -> StageShrink -> UpgradePhase
            _scheduler.Tick(20f);
            _scheduler.Tick(1f);
            Assert.AreEqual(SchedulerState.UpgradePhase, _scheduler.State);

            // Both players skip
            _draftP1.Skip();
            _draftP2.Skip();

            // Tick to process completion
            _scheduler.Tick(0.1f);
            Assert.AreEqual(SchedulerState.Running, _scheduler.State);
        }

        [Test]
        public void UpgradePhase_Timeout_AutoSkips()
        {
            // Running -> StageShrink -> UpgradePhase
            _scheduler.Tick(20f);
            _scheduler.Tick(1f);
            Assert.AreEqual(SchedulerState.UpgradePhase, _scheduler.State);

            // Tick past the 10s upgrade selection timeout
            _scheduler.Tick(10.1f);

            // Both should be timed out and scheduler back to Running
            Assert.AreNotEqual(DraftState.Choosing, _draftP1.State.CurrentValue);
            Assert.AreNotEqual(DraftState.Choosing, _draftP2.State.CurrentValue);
            Assert.AreEqual(SchedulerState.Running, _scheduler.State);
        }

        [Test]
        public void PlayerDeath_TransitionsToResult()
        {
            // Kill player 1 by direct damage
            _player1.Stats.TakeDamage(10);

            // Tick so scheduler checks end condition
            _scheduler.Tick(0.1f);
            Assert.AreEqual(SchedulerState.Result, _scheduler.State);
            Assert.AreEqual(GamePhase.Result, _scheduler.Clock.CurrentPhaseValue);
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
            public int BombMinFlightDistance => 3;
            public float StageShrinkAnimDuration => 1f;
            public float FireBombSpreadInterval => 0.15f;
            public float FallBombSpreadInterval => 0.3f;
            public int HpRecoveryAmount => 3;
            public int HpRecoveryThreshold => 5;
        }
    }
}
