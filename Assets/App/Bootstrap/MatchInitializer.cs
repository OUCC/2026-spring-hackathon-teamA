using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using VContainer.Unity;
using FloorBreaker.Shared.Domain.Grid;
using FloorBreaker.Shared.Domain.Primitives;
using FloorBreaker.Shared.Domain.Timing;
using FloorBreaker.Shared.Application.Interfaces;
using FloorBreaker.Stage.Domain;
using FloorBreaker.Stage.Presentation;
using FloorBreaker.Player.Presentation;
using FloorBreaker.Bombs.Application;
using FloorBreaker.Bombs.Presentation;
using FloorBreaker.Slimes.Domain;
using FloorBreaker.Slimes.Presentation;
using FloorBreaker.Upgrades.Domain;
using FloorBreaker.MatchFlow.Application;
using FloorBreaker.Cameras.Presentation;
using FloorBreaker.Input.Application;
using FloorBreaker.Input.Infrastructure;
using FloorBreaker.UI.RuntimeUI.Documents;
using FloorBreaker.UI.HUD.Presentation;
using FloorBreaker.UI.UpgradeOverlay.Presentation;
using FloorBreaker.UI.Result.Presentation;

namespace FloorBreaker.Bootstrap
{
    /// <summary>
    /// Match シーンの初期化シーケンスを実行する EntryPoint。
    /// VContainer から全依存を注入され、ランタイム初期化のみを行う。
    /// </summary>
    public sealed class MatchInitializer : IAsyncStartable
    {
        private readonly IBalanceParameters _balance;
        private readonly IRandomProvider _random;
        private readonly StageModel _stage;
        private readonly WallGenerationService _wallGen;
        private readonly MatchPlayers _players;
        private readonly SlimeSpawnService _slimeSpawnService;
        private readonly SlimeRegistry _slimeRegistry;
        private readonly StageViewFactory _stageViewFactory;
        private readonly TileViewRegistry _tileViewRegistry;
        private readonly PlayerViewFactory _playerViewFactory;
        private readonly BombViewFactory _bombViewFactory;
        private readonly SlimeViewFactory _slimeViewFactory;
        private readonly SplitScreenCameraSetup _cameraSetup;
        private readonly MatchUIDocument _matchUIDocument;
        private readonly TileAnimationService _tileAnimService;
        private readonly PlayerAnimationService _playerAnimService;
        private readonly BombAnimationService _bombAnimService;
        private readonly SlimeAnimationService _slimeAnimService;
        private readonly StageQueryService _stageQuery;
        private readonly BombFlightTracker _bombFlightTracker;
        private readonly MatchClock _clock;
        private readonly UpgradePhaseUseCase _upgradePhase;
        private readonly MatchEndUseCase _matchEnd;
        private readonly UpgradeSelectionState _selectionState;
        private readonly MatchPresenters _presenters;
        private readonly GameplayInputBridge _gameplayInputBridge;
        private readonly UpgradeUIInputBridge _upgradeUIInputBridge;

        public MatchInitializer(
            IBalanceParameters balance,
            IRandomProvider random,
            StageModel stage,
            WallGenerationService wallGen,
            MatchPlayers players,
            SlimeSpawnService slimeSpawnService,
            SlimeRegistry slimeRegistry,
            StageViewFactory stageViewFactory,
            TileViewRegistry tileViewRegistry,
            PlayerViewFactory playerViewFactory,
            BombViewFactory bombViewFactory,
            SlimeViewFactory slimeViewFactory,
            SplitScreenCameraSetup cameraSetup,
            MatchUIDocument matchUIDocument,
            TileAnimationService tileAnimService,
            PlayerAnimationService playerAnimService,
            BombAnimationService bombAnimService,
            SlimeAnimationService slimeAnimService,
            StageQueryService stageQuery,
            BombFlightTracker bombFlightTracker,
            MatchClock clock,
            UpgradePhaseUseCase upgradePhase,
            MatchEndUseCase matchEnd,
            UpgradeSelectionState selectionState,
            MatchPresenters presenters,
            GameplayInputBridge gameplayInputBridge,
            UpgradeUIInputBridge upgradeUIInputBridge)
        {
            _balance = balance;
            _random = random;
            _stage = stage;
            _wallGen = wallGen;
            _players = players;
            _slimeSpawnService = slimeSpawnService;
            _slimeRegistry = slimeRegistry;
            _stageViewFactory = stageViewFactory;
            _tileViewRegistry = tileViewRegistry;
            _playerViewFactory = playerViewFactory;
            _bombViewFactory = bombViewFactory;
            _slimeViewFactory = slimeViewFactory;
            _cameraSetup = cameraSetup;
            _matchUIDocument = matchUIDocument;
            _tileAnimService = tileAnimService;
            _playerAnimService = playerAnimService;
            _bombAnimService = bombAnimService;
            _slimeAnimService = slimeAnimService;
            _stageQuery = stageQuery;
            _bombFlightTracker = bombFlightTracker;
            _clock = clock;
            _upgradePhase = upgradePhase;
            _matchEnd = matchEnd;
            _selectionState = selectionState;
            _presenters = presenters;
            _gameplayInputBridge = gameplayInputBridge;
            _upgradeUIInputBridge = upgradeUIInputBridge;
        }

        public async UniTask StartAsync(CancellationToken ct)
        {
            // 1. 壁生成
            var bounds = _stage.GetCurrentBounds();
            var p1Spawn = _players.Player1.CurrentPosition;
            var p2Spawn = _players.Player2.CurrentPosition;
            var walls = _wallGen.Generate(bounds, p1Spawn, p2Spawn, _random);
            foreach (var pos in walls)
                _stage.SetTileState(pos, TileState.Wall);

            // 2. TileView 生成
            var tileViews = _stageViewFactory.CreateTileViews(_stage, bounds);
            _tileViewRegistry.SetViews(tileViews);

            // 3. TileFireVfxPool 生成
            var stageConfig = _stageViewFactory.Config;
            var fireVfxPrefab = stageConfig.FireVfxPrefab;
            var fireVfxPool = new TileFireVfxPool(
                fireVfxPrefab, _stageViewFactory.transform);

            // 4. StagePresenter 生成
            var stagePresenter = new StagePresenter(
                _stage, tileViews, _tileAnimService, fireVfxPool, stageConfig);
            _presenters.Stage = stagePresenter;

            // 5. StageShrinkAnimator 生成
            var shrinkAnimator = new StageShrinkAnimator(
                _stage, tileViews, _tileAnimService, stageConfig,
                _balance.StageShrinkAnimDuration);
            stagePresenter.SetShrinkAnimator(shrinkAnimator);
            _presenters.ShrinkAnimator = shrinkAnimator;

            // 6. PlayerView + PlayerPresenter 生成
            var playerConfig = _playerViewFactory.Config;

            var p1View = _playerViewFactory.CreatePlayerView(
                PlayerId.Player1, p1Spawn);
            _presenters.PlayerP1 = new PlayerPresenter(
                _players.Player1, p1View, _playerAnimService, playerConfig);

            var p2View = _playerViewFactory.CreatePlayerView(
                PlayerId.Player2, p2Spawn);
            _presenters.PlayerP2 = new PlayerPresenter(
                _players.Player2, p2View, _playerAnimService, playerConfig);

            // 7. BombExplosionVfxPool + BombPresenter 生成
            var bombConfig = _bombViewFactory.Config;
            var bombVfxPool = new BombExplosionVfxPool(
                bombConfig.GetExplosionPrefab(BombType.Fire),
                bombConfig.GetExplosionPrefab(BombType.Fall),
                _bombViewFactory.transform,
                bombConfig.ExplosionVfxScale,
                bombConfig.ExplosionVfxDuration);
            _presenters.Bomb = new BombPresenter(
                _bombFlightTracker, _bombViewFactory, _bombAnimService,
                bombVfxPool, bombConfig, _stageQuery, tileViews,
                _balance.BombFlightSpeed);

            // 8. SlimePresenter 生成
            var slimeConfig = _slimeViewFactory.Config;
            _presenters.Slime = new SlimePresenter(
                _slimeRegistry, _slimeViewFactory, _slimeAnimService, slimeConfig);

            // 9. カメラセットアップ
            _cameraSetup.Initialize(_players.Player1, _players.Player2, _stage.Bounds);

            // 10. 初期スライムスポーン
            _slimeSpawnService.SpawnIfNeeded(
                _stage, _slimeRegistry, _players.All, _random, _balance);

            // 11. HUD Presenter 生成
            var hudViewP1 = new PlayerHudView(_matchUIDocument.LeftHudRoot);
            _presenters.HudP1 = new PlayerHudPresenter(
                hudViewP1, _players.Player1.Stats, _players.Player1.Build,
                _players.Cooldown1, _clock);

            var hudViewP2 = new PlayerHudView(_matchUIDocument.RightHudRoot);
            _presenters.HudP2 = new PlayerHudPresenter(
                hudViewP2, _players.Player2.Stats, _players.Player2.Build,
                _players.Cooldown2, _clock);

            // 12. UpgradeOverlay Presenter 生成
            var overlayView = new UpgradeOverlayView(_matchUIDocument.UpgradeOverlayRoot);
            _presenters.UpgradeOverlay = new UpgradeOverlayPresenter(
                overlayView, _clock, _upgradePhase, _selectionState,
                _players.Player1.Stats, _players.Player2.Stats,
                _matchUIDocument.UpgradeCardTemplate);

            // 13. Result Presenter 生成
            var resultView = new ResultView(_matchUIDocument.ResultRoot);
            _presenters.Result = new ResultPresenter(resultView, _clock, _matchEnd);

            // 14. Input 配線
            var inputAdapters = Object.FindObjectsByType<PlayerInputAdapter>(
                FindObjectsSortMode.None);
            InputActionAsset inputActions = null;

            // P1, P2 の順に割り当て
            for (int i = 0; i < inputAdapters.Length && i < 2; i++)
            {
                var adapter = inputAdapters[i];
                var id = i == 0 ? PlayerId.Player1 : PlayerId.Player2;
                adapter.Initialize(id);

                _gameplayInputBridge.RegisterAdapter(adapter);

                // InputActionAsset を取得 (全アダプターで共通)
                var pi = adapter.GetComponent<PlayerInput>();
                if (pi != null && inputActions == null)
                    inputActions = pi.actions;
            }

            // 15. InputMapSwitcher 生成
            if (inputActions != null)
            {
                _presenters.InputMapSwitcher = new InputMapSwitcher(inputActions, _clock);

                // 16. UpgradeUI アクションマップとの接続
                var upgradeP1 = inputActions.FindActionMap("UpgradeUI_P1");
                var upgradeP2 = inputActions.FindActionMap("UpgradeUI_P2");

                if (upgradeP1 != null)
                {
                    upgradeP1["Navigate"].performed += _upgradeUIInputBridge.OnNavigateP1;
                    upgradeP1["Submit"].performed += _upgradeUIInputBridge.OnSubmitP1;
                    upgradeP1["Skip"].performed += _upgradeUIInputBridge.OnSkipP1;
                    upgradeP1["Reroll"].performed += _upgradeUIInputBridge.OnRerollP1;
                }

                if (upgradeP2 != null)
                {
                    upgradeP2["Navigate"].performed += _upgradeUIInputBridge.OnNavigateP2;
                    upgradeP2["Submit"].performed += _upgradeUIInputBridge.OnSubmitP2;
                    upgradeP2["Skip"].performed += _upgradeUIInputBridge.OnSkipP2;
                    upgradeP2["Reroll"].performed += _upgradeUIInputBridge.OnRerollP2;
                }
            }

            await UniTask.CompletedTask;
        }
    }
}
