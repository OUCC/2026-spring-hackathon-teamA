using System;
using FloorBreaker.Shared.Domain.Grid;
using FloorBreaker.Shared.Domain.Primitives;
using FloorBreaker.Shared.Domain.Timing;
using FloorBreaker.Shared.Application.Interfaces;
using FloorBreaker.Stage.Domain;
using FloorBreaker.Stage.Presentation;
using FloorBreaker.Player.Domain;
using FloorBreaker.Player.Presentation;
using FloorBreaker.Bombs.Application;
using FloorBreaker.Bombs.Presentation;
using FloorBreaker.Slimes.Domain;
using FloorBreaker.Slimes.Presentation;
using FloorBreaker.Upgrades.Domain;
using FloorBreaker.MatchFlow.Application;
using FloorBreaker.Shared.Presentation.Common;
using FloorBreaker.Cameras.Presentation;
using FloorBreaker.UI.RuntimeUI.Documents;
using FloorBreaker.UI.HUD.Presentation;
using FloorBreaker.UI.UpgradeOverlay.Presentation;
using FloorBreaker.UI.Result.Presentation;

namespace FloorBreaker.Bootstrap
{
    /// <summary>
    /// Presentation 層の初期化を担当する。
    /// TileView / Presenter / HUD / Overlay / Result を生成し、MatchPresenters に格納する。
    /// </summary>
    public sealed class PresentationInitializer
    {
        private readonly StageModel _stage;
        private readonly StageViewFactory _stageViewFactory;
        private readonly TileViewRegistry _tileViewRegistry;
        private readonly TileAnimationService _tileAnimService;
        private readonly PlayerViewFactory _playerViewFactory;
        private readonly PlayerAnimationService _playerAnimService;
        private readonly BombViewFactory _bombViewFactory;
        private readonly BombAnimationService _bombAnimService;
        private readonly BombFlightTracker _bombFlightTracker;
        private readonly StageQueryService _stageQuery;
        private readonly SlimeViewFactory _slimeViewFactory;
        private readonly SlimeAnimationService _slimeAnimService;
        private readonly SlimeRegistry _slimeRegistry;
        private readonly MatchUIDocument _matchUIDocument;
        private readonly MatchPlayers _players;
        private readonly MatchClock _clock;
        private readonly UpgradePhaseUseCase _upgradePhase;
        private readonly MatchEndUseCase _matchEnd;
        private readonly UpgradeSelectionState _selectionState;
        private readonly IBalanceParameters _balance;
        private readonly IAudioService _audio;
        private readonly ICameraShakeService _cameraShake;
        private readonly ImpactFreezeService _impactFreeze;
        private readonly ISceneTransitionService _sceneTransition;
        private readonly TileTimerService _tileTimerService;
        private readonly MatchModeConfig _modeConfig;
        private readonly MatchPresenters _presenters;

        public PresentationInitializer(
            StageModel stage,
            StageViewFactory stageViewFactory,
            TileViewRegistry tileViewRegistry,
            TileAnimationService tileAnimService,
            PlayerViewFactory playerViewFactory,
            PlayerAnimationService playerAnimService,
            BombViewFactory bombViewFactory,
            BombAnimationService bombAnimService,
            BombFlightTracker bombFlightTracker,
            StageQueryService stageQuery,
            SlimeViewFactory slimeViewFactory,
            SlimeAnimationService slimeAnimService,
            SlimeRegistry slimeRegistry,
            MatchUIDocument matchUIDocument,
            MatchPlayers players,
            MatchClock clock,
            UpgradePhaseUseCase upgradePhase,
            MatchEndUseCase matchEnd,
            UpgradeSelectionState selectionState,
            IBalanceParameters balance,
            IAudioService audio,
            ICameraShakeService cameraShake,
            ImpactFreezeService impactFreeze,
            ISceneTransitionService sceneTransition,
            TileTimerService tileTimerService,
            MatchModeConfig modeConfig,
            MatchPresenters presenters)
        {
            _stage = stage;
            _stageViewFactory = stageViewFactory;
            _tileViewRegistry = tileViewRegistry;
            _tileAnimService = tileAnimService;
            _playerViewFactory = playerViewFactory;
            _playerAnimService = playerAnimService;
            _bombViewFactory = bombViewFactory;
            _bombAnimService = bombAnimService;
            _bombFlightTracker = bombFlightTracker;
            _stageQuery = stageQuery;
            _slimeViewFactory = slimeViewFactory;
            _slimeAnimService = slimeAnimService;
            _slimeRegistry = slimeRegistry;
            _matchUIDocument = matchUIDocument;
            _players = players;
            _clock = clock;
            _upgradePhase = upgradePhase;
            _matchEnd = matchEnd;
            _selectionState = selectionState;
            _balance = balance;
            _audio = audio;
            _cameraShake = cameraShake;
            _impactFreeze = impactFreeze;
            _sceneTransition = sceneTransition;
            _tileTimerService = tileTimerService;
            _modeConfig = modeConfig;
            _presenters = presenters;
        }

        /// <summary>
        /// TileView 生成から Result Presenter まで、全 Presentation を初期化する。
        /// </summary>
        public void Initialize()
        {
            var bounds = _stage.GetCurrentBounds();

            // 1. TileView 生成
            var tileViews = _stageViewFactory.CreateTileViews(_stage, bounds);
            _tileViewRegistry.SetViews(tileViews);

            // 2. TileFireVfxPool 生成
            var stageConfig = _stageViewFactory.Config;
            var fireVfxPrefab = stageConfig.FireVfxPrefab;
            var fireVfxPool = new TileFireVfxPool(
                fireVfxPrefab, _stageViewFactory.transform);

            // 3. StagePresenter 生成
            var stagePresenter = new StagePresenter(
                _stage, tileViews, _tileAnimService, fireVfxPool, stageConfig, _audio);
            stagePresenter.SetTileTimerService(_tileTimerService);
            _presenters.Stage = stagePresenter;

            // 4. StageShrinkAnimator 生成
            var shrinkAnimator = new StageShrinkAnimator(
                _stage, tileViews, _tileAnimService, stageConfig,
                _balance.StageShrinkAnimDuration, _cameraShake, _audio);
            stagePresenter.SetShrinkAnimator(shrinkAnimator);
            _presenters.ShrinkAnimator = shrinkAnimator;

            // 4b. ShrinkWarningPresenter 生成
            _presenters.ShrinkWarning = new ShrinkWarningPresenter(
                _clock, _stage.Bounds, tileViews, _tileAnimService, stageConfig);

            // 5. PlayerView + PlayerPresenter 生成 (N-player loop)
            var playerConfig = _playerViewFactory.Config;
            var playerPresenters = new PlayerPresenter[_players.PlayerCount];
            for (int i = 0; i < _players.PlayerCount; i++)
            {
                var player = _players.All[i];
                var view = _playerViewFactory.CreatePlayerView(player.Id, player.CurrentPosition);
                playerPresenters[i] = new PlayerPresenter(
                    player, view, _playerAnimService, playerConfig, _audio, _cameraShake, _impactFreeze);
            }
            _presenters.Players = playerPresenters;

            // 6. BombExplosionVfxPool + BombPresenter 生成
            var bombConfig = _bombViewFactory.Config;
            var bombVfxPool = new BombExplosionVfxPool(
                bombConfig.GetExplosionPrefab(BombType.Fire),
                bombConfig.GetExplosionPrefab(BombType.Break),
                _bombViewFactory.transform,
                bombConfig.ExplosionVfxScale,
                bombConfig.ExplosionVfxDuration);
            _presenters.Bomb = new BombPresenter(
                _bombFlightTracker, _bombViewFactory, _bombAnimService,
                bombVfxPool, bombConfig, _stageQuery, tileViews,
                _balance.BombFlightSpeed, _audio, _cameraShake, _impactFreeze);

            // 7. SlimePresenter 生成
            var slimeConfig = _slimeViewFactory.Config;
            _presenters.Slime = new SlimePresenter(
                _slimeRegistry, _slimeViewFactory, _slimeAnimService, slimeConfig, _audio, _cameraShake);

            // 8. ImpactFreezeService にフラッシュオーバーレイを設定
            _impactFreeze?.SetFlashOverlay(_matchUIDocument.ImpactFlashOverlay);

            // 8b. N-player ペイン動的生成
            _matchUIDocument.CreatePanes(_players.PlayerCount);

            // 9. HUD Presenter 生成 (N-player)
            var hudRoots = _matchUIDocument.HudRoots;
            var huds = new PlayerHudPresenter[_players.PlayerCount];
            for (int i = 0; i < _players.PlayerCount; i++)
            {
                var hudView = new PlayerHudView(hudRoots[i]);
                huds[i] = new PlayerHudPresenter(
                    hudView, _players.All[i].Stats, _players.All[i].Build,
                    _players.Cooldowns[i], _clock);
            }
            _presenters.Huds = huds;

            // 10. UpgradeOverlay Presenter 生成 (N-player)
            var overlayView = new UpgradeOverlayView(
                _matchUIDocument.UpgradeOverlayRoot, _matchUIDocument.UpgradePanes);
            var allStats = new PlayerStats[_players.PlayerCount];
            for (int i = 0; i < _players.PlayerCount; i++)
                allStats[i] = _players.All[i].Stats;
            _presenters.UpgradeOverlay = new UpgradeOverlayPresenter(
                overlayView, _clock, _upgradePhase, _selectionState,
                allStats, _matchUIDocument.UpgradeCardTemplate, _audio);

            // 11. Result Presenter 生成 (N-player)
            var resultView = new ResultView(
                _matchUIDocument.ResultRoot, _matchUIDocument.ResultPanes);
            _presenters.Result = new ResultPresenter(resultView, _clock, _matchEnd, _players.PlayerCount, _sceneTransition, _modeConfig);
        }
    }
}
