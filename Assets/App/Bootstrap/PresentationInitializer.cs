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
using R3;
using FloorBreaker.UI.HUD.Presentation;
using FloorBreaker.UI.UpgradeOverlay.Presentation;
using FloorBreaker.UI.Pause.Presentation;
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
        private readonly MatchPhaseScheduler _scheduler;
        private readonly SplitScreenCameraSetup _cameraSetup;
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
            MatchPhaseScheduler scheduler,
            SplitScreenCameraSetup cameraSetup,
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
            _scheduler = scheduler;
            _cameraSetup = cameraSetup;
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

            // 8b. Human プレイヤーインデックスを算出
            var humanIndices = new System.Collections.Generic.List<int>();
            for (int i = 0; i < _players.PlayerCount; i++)
                if (!_modeConfig.IsCpuAt(i)) humanIndices.Add(i);
            int humanCount = humanIndices.Count;

            // 8c. Human 分のみペイン動的生成
            _matchUIDocument.CreatePanes(humanCount);

            // 9. HUD Presenter 生成 (Human のみ)
            var iconMap = _matchUIDocument.UpgradeIconMap;
            var hudRoots = _matchUIDocument.HudRoots;
            var huds = new PlayerHudPresenter[humanCount];
            for (int h = 0; h < humanCount; h++)
            {
                int idx = humanIndices[h];
                var hudView = new PlayerHudView(hudRoots[h]);
                huds[h] = new PlayerHudPresenter(
                    hudView, _players.All[idx].Stats, _players.All[idx].Build,
                    _players.Cooldowns[idx], _clock, iconMap);
            }
            _presenters.Huds = huds;

            // 10. UpgradeOverlay Presenter 生成 (Human のみ、インデックスマッピング付き)
            var overlayView = new UpgradeOverlayView(
                _matchUIDocument.UpgradeOverlayRoot, _matchUIDocument.UpgradePanes);
            var humanStats = new PlayerStats[humanCount];
            for (int h = 0; h < humanCount; h++)
                humanStats[h] = _players.All[humanIndices[h]].Stats;
            _presenters.UpgradeOverlay = new UpgradeOverlayPresenter(
                overlayView, _clock, _upgradePhase, _selectionState,
                humanStats, _matchUIDocument.UpgradeCardTemplate, _audio,
                humanIndices.ToArray(), iconMap);

            // 11. Result Presenter 生成 (Human のみ)
            var resultView = new ResultView(
                _matchUIDocument.ResultRoot, _matchUIDocument.ResultPanes);
            _presenters.Result = new ResultPresenter(resultView, _clock, _matchEnd, _players.PlayerCount, _sceneTransition, _modeConfig, humanIndices.ToArray());

            // 12. PauseOverlay Presenter 生成
            var pauseView = new PauseOverlayView(_matchUIDocument.PauseOverlayRoot);
            _presenters.Pause = new PauseOverlayPresenter(pauseView, _clock, _scheduler, _sceneTransition);

            // 13. Human 死亡時の観戦カメラ切り替え + アップグレード UI 非表示
            for (int h = 0; h < humanCount; h++)
            {
                int cameraIndex = h;
                int paneIndex = h;
                int playerIdx = humanIndices[h];
                var devType = _modeConfig.DeviceTypes[playerIdx];
                int gpIdx = _modeConfig.GamepadIndices[playerIdx];
                var sub = _players.All[playerIdx].Stats.CurrentHp.Subscribe(hp =>
                {
                    if (hp <= 0)
                    {
                        _cameraSetup.ConvertToSpectator(
                            cameraIndex, _stage.Bounds, _players.All, devType, gpIdx);
                        _presenters.UpgradeOverlay?.DisablePane(paneIndex);
                    }
                });
                _presenters.Subscriptions.Add(sub);
            }
        }
    }
}
