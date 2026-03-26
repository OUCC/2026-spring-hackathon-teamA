using System;
using System.Collections.Generic;
using FloorBreaker.Shared.Domain.Grid;
using FloorBreaker.Shared.Domain.Primitives;
using FloorBreaker.Shared.Domain.Timing;
using FloorBreaker.Shared.Application.Interfaces;
using FloorBreaker.Stage.Domain;
using FloorBreaker.Player.Domain;
using FloorBreaker.Bombs.Domain;
using FloorBreaker.Bombs.Application;
using FloorBreaker.Slimes.Domain;
using FloorBreaker.Slimes.Application;
using FloorBreaker.Upgrades.Domain;

namespace FloorBreaker.MatchFlow.Application
{
    /// <summary>
    /// マッチ全体の初期化と統括。
    /// Bootstrap (Phase 15) から呼ばれる。
    /// </summary>
    public sealed class MatchFlowOrchestrator : IDisposable
    {
        private readonly IBalanceParameters _balance;
        private readonly IRandomProvider _random;

        // 所有するインスタンス群
        private StageModel _stage;
        private TileTimerService _tileTimerService;
        private PlayerModel _player1;
        private PlayerModel _player2;
        private BombCooldownState _p1Cooldown;
        private BombCooldownState _p2Cooldown;
        private SlimeRegistry _slimeRegistry;
        private MatchClock _clock;
        private MatchPhaseScheduler _scheduler;
        private SlimeTickService _slimeTickService;
        private BombFlightTracker _bombFlightTracker;

        public MatchPhaseScheduler Scheduler => _scheduler;
        public StageModel Stage => _stage;
        public PlayerModel Player1 => _player1;
        public PlayerModel Player2 => _player2;

        public MatchFlowOrchestrator(IBalanceParameters balance, IRandomProvider random)
        {
            _balance = balance;
            _random = random;
        }

        public void Initialize()
        {
            int size = _balance.StageSize;

            // 1. ステージ生成
            var bounds = TileCoordRange.FromSize(size);
            _stage = new StageModel(bounds);
            _tileTimerService = new TileTimerService(_stage);

            var wallGen = new WallGenerationService(
                _balance.WallSeedPercent, _balance.WallGrowthChance,
                _balance.WallTargetPercent, _balance.SpawnProtectionRadius);

            var p1Spawn = new GridPos(_balance.SpawnProtectionRadius, _balance.SpawnProtectionRadius);
            var p2Spawn = new GridPos(size - 1 - _balance.SpawnProtectionRadius,
                                      size - 1 - _balance.SpawnProtectionRadius);

            var walls = wallGen.Generate(bounds, p1Spawn, p2Spawn, _random);
            foreach (var wallPos in walls)
                _stage.SetTileState(wallPos, TileState.Wall);

            // 2. プレイヤー生成
            var stats1 = new PlayerStats(_balance.InitialHp, _balance.BaseMovementSpeed, _balance.MaxMovementSpeed);
            var build1 = CreateDefaultBuild();
            _player1 = new PlayerModel(PlayerId.Player1, p1Spawn, stats1, build1);

            var stats2 = new PlayerStats(_balance.InitialHp, _balance.BaseMovementSpeed, _balance.MaxMovementSpeed);
            var build2 = CreateDefaultBuild();
            _player2 = new PlayerModel(PlayerId.Player2, p2Spawn, stats2, build2);

            var players = new List<PlayerModel> { _player1, _player2 };

            // 3. ボムクールダウン
            _p1Cooldown = new BombCooldownState();
            _p2Cooldown = new BombCooldownState();

            // 4. スライム
            _slimeRegistry = new SlimeRegistry();
            var spawnService = new SlimeSpawnService();
            spawnService.SpawnIfNeeded(_stage, _slimeRegistry, players, _random, _balance);

            var safeTileSearch = new SafeTileSearchService();
            var damageService = new PlayerDamageService(
                _balance.InvulnerabilityDuration, _balance.ForcedMoveDuration);
            var aiService = new SlimeAiService(damageService, safeTileSearch);
            _slimeTickService = new SlimeTickService(aiService, spawnService, _slimeRegistry, _tileTimerService);

            // 5. 強化
            var catalog = new UpgradeCatalog();
            var availRule = new UpgradeAvailabilityRule(_balance);
            var rollRule = new UpgradeRollRule(catalog, availRule);
            var applyService = new UpgradeApplyService(_balance);
            var draftP1 = new UpgradeDraftService(rollRule, applyService, _balance);
            var draftP2 = new UpgradeDraftService(rollRule, applyService, _balance);

            // 6. ボム
            var stageQuery = new StageQueryService(_stage);
            var areaResolver = new BombAreaResolver(stageQuery);
            var landingResolver = new BombLandingResolver(_stage);
            var fallResolver = new FallBombResolver(areaResolver);
            var fireResolver = new FireBombResolver(areaResolver);
            var slimeDropResolver = new SlimeDropResolver(catalog, applyService);
            var spreadService = new BombEffectSpreadService(
                _stage, _tileTimerService, damageService, safeTileSearch,
                _slimeRegistry, slimeDropResolver, _random);
            var launchUseCase = new BombLaunchUseCase(
                landingResolver, fallResolver, fireResolver,
                _stage, _balance, spreadService);

            _bombFlightTracker = new BombFlightTracker(
                launchUseCase, _p1Cooldown, _p2Cooldown,
                _stage, _slimeRegistry, _balance);

            // 7. MatchFlow サービス
            var fireDamageTickService = new FireDamageTickService(
                damageService, safeTileSearch, _slimeRegistry, _balance);
            var shrinkService = new StageShrinkService();
            var upgradePhaseUseCase = new UpgradePhaseUseCase(draftP1, draftP2, _balance);
            var matchEndUseCase = new MatchEndUseCase();

            _clock = new MatchClock(_balance.PhaseDuration);

            // 8. スケジューラ生成
            _scheduler = new MatchPhaseScheduler(
                _clock, _tileTimerService, _p1Cooldown, _p2Cooldown,
                _slimeTickService, fireDamageTickService, _bombFlightTracker,
                spreadService, shrinkService, upgradePhaseUseCase, matchEndUseCase,
                damageService, safeTileSearch, players, _stage,
                _slimeRegistry, _balance, _random);
        }

        private PlayerBuild CreateDefaultBuild()
        {
            return new PlayerBuild(
                _balance.FireBombMaxFlightDistance, _balance.FireBombEffectRange,
                _balance.FireBombContactDamage, _balance.FireBombCooldown,
                _balance.FireBombDuration, _balance.FireBombDefaultWallPenetration,
                _balance.FireBombCooldownMin,
                _balance.FallBombMaxFlightDistance, _balance.FallBombEffectRange,
                _balance.FallBombDamage, _balance.FallBombCooldown,
                _balance.FallBombCollapseDuration, _balance.FallBombCooldownMin);
        }

        public void Dispose()
        {
            _bombFlightTracker?.Dispose();
            _slimeTickService?.Dispose();
            _p1Cooldown?.Dispose();
            _p2Cooldown?.Dispose();
            _player1?.Dispose();
            _player2?.Dispose();
            _tileTimerService?.Dispose();
            _stage?.Dispose();
            _clock?.Dispose();
        }
    }
}
