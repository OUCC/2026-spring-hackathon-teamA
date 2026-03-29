using System.Threading;
using Cysharp.Threading.Tasks;
using R3;
using VContainer.Unity;
using FloorBreaker.Shared.Domain.Primitives;
using FloorBreaker.Shared.Domain.Timing;
using FloorBreaker.Shared.Application.Interfaces;
using FloorBreaker.Stage.Domain;
using FloorBreaker.ScriptableObjects.Configs;
using FloorBreaker.Slimes.Domain;
using FloorBreaker.Player.Domain;
using FloorBreaker.MatchFlow.Application;
using FloorBreaker.Cameras.Presentation;
namespace FloorBreaker.Bootstrap
{
    /// <summary>
    /// Match シーンの初期化シーケンスを実行する EntryPoint。
    /// Domain 初期化を行い、Presentation / Input の初期化は専用クラスへ委譲する。
    /// </summary>
    public sealed class MatchInitializer : IAsyncStartable, System.IDisposable
    {
        private readonly IBalanceParameters _balance;
        private readonly IRandomProvider _random;
        private readonly StageModel _stage;
        private readonly StageGenerationService _stageGen;
        private readonly MatchPlayers _players;
        private readonly SlimeSpawnService _slimeSpawnService;
        private readonly SplitScreenCameraSetup _cameraSetup;
        private readonly PresentationInitializer _presentationInit;
        private readonly InputInitializer _inputInit;
        private readonly MatchClock _clock;
        private readonly IAudioService _audio;
        private readonly StageConfig _stageConfig;
        private readonly WarpService _warpService;
        private readonly MatchModeConfig _modeConfig;

        // Dispose 用: フェーズ SE 購読の解除
        private System.IDisposable _phaseSub;

        public MatchInitializer(
            IBalanceParameters balance,
            IRandomProvider random,
            StageModel stage,
            StageGenerationService stageGen,
            MatchPlayers players,
            SlimeSpawnService slimeSpawnService,
            SplitScreenCameraSetup cameraSetup,
            PresentationInitializer presentationInit,
            InputInitializer inputInit,
            MatchClock clock,
            IAudioService audio,
            StageConfig stageConfig,
            WarpService warpService,
            MatchModeConfig modeConfig)
        {
            _balance = balance;
            _random = random;
            _stage = stage;
            _stageGen = stageGen;
            _players = players;
            _slimeSpawnService = slimeSpawnService;
            _cameraSetup = cameraSetup;
            _presentationInit = presentationInit;
            _inputInit = inputInit;
            _clock = clock;
            _audio = audio;
            _stageConfig = stageConfig;
            _warpService = warpService;
            _modeConfig = modeConfig;
        }

        public async UniTask StartAsync(CancellationToken ct)
        {
            // 0. BGM 切替 (タイトル BGM 停止 → ゲーム BGM 開始)
            _audio?.StopBgm(0.3f);
            _audio?.PlayBgm(SfxIds.BgmMatch);

            // 1. ステージ生成 (壁 → ガス脈 → プリセット)
            var spawnPositions = new System.Collections.Generic.List<FloorBreaker.Shared.Domain.Grid.GridPos>();
            foreach (var p in _players.All) spawnPositions.Add(p.CurrentPosition);

            if (_stageConfig != null)
            {
                var genParams = new StageGenerationParams
                {
                    WallSeedPercent = _stageConfig.WallSeedPercent,
                    WallGrowthChance = _stageConfig.WallGrowthChance,
                    WallTargetPercent = _stageConfig.WallTargetPercent,
                    SpawnProtectionRadius = _stageConfig.SpawnProtectionRadius,
                    GasVeinCount = _stageConfig.GasVeinCount,
                    GasVeinMinLength = _stageConfig.GasVeinMinLength,
                    GasVeinMaxLength = _stageConfig.GasVeinMaxLength,
                    PresetTiles = _stageConfig.PresetTiles,
                };
                _stageGen.PopulateStage(_stage, genParams, spawnPositions, _random);
            }

            // 1d. WarpService レジストリ構築
            _warpService?.BuildRegistry(_stage.GetCurrentBounds());

            // 2. Presentation 初期化 (TileView → Presenter → HUD → Overlay → Result)
            _presentationInit.Initialize();

            // 3. カメラセットアップ (Human プレイヤーのみ)
            var humanPlayers = new System.Collections.Generic.List<PlayerModel>();
            for (int i = 0; i < _players.PlayerCount; i++)
                if (!_modeConfig.IsCpuAt(i)) humanPlayers.Add(_players.All[i]);

            if (humanPlayers.Count > 0)
                _cameraSetup.Initialize(humanPlayers, _stage.Bounds);
            else
                _cameraSetup.InitializeSpectator(_stage.Bounds, _players.All);

            // 4. 初期スライムスポーン
            _slimeSpawnService.SpawnIfNeeded();

            // 5. Input 配線 (PlayerInputAdapter → InputMapSwitcher → UpgradeUI)
            _inputInit.Initialize();

            // 6. フェーズ遷移 SE
            _phaseSub = _clock.CurrentPhase.Subscribe(phase =>
            {
                switch (phase)
                {
                    case GamePhase.StageShrink:
                        _audio?.PlaySfx(SfxIds.PhaseShrink);
                        break;
                    case GamePhase.UpgradePhase:
                        _audio?.PlaySfx(SfxIds.PhaseUpgrade);
                        _audio?.DuckBgm(0.3f, 0.5f);
                        break;
                    case GamePhase.MatchRunning:
                        _audio?.DuckBgm(1.0f, 0.5f);
                        break;
                    case GamePhase.Result:
                        _audio?.PlaySfx(SfxIds.MatchResult);
                        break;
                }
            });

            await UniTask.CompletedTask;
        }

        public void Dispose()
        {
            _phaseSub?.Dispose();
            _inputInit?.Dispose();
        }
    }
}
