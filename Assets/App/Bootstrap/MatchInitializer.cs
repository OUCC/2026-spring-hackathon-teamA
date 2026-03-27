using System.Threading;
using Cysharp.Threading.Tasks;
using R3;
using VContainer.Unity;
using FloorBreaker.Shared.Domain.Primitives;
using FloorBreaker.Shared.Domain.Timing;
using FloorBreaker.Shared.Application.Interfaces;
using FloorBreaker.Stage.Domain;
using FloorBreaker.Slimes.Domain;
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
        private readonly WallGenerationService _wallGen;
        private readonly MatchPlayers _players;
        private readonly SlimeSpawnService _slimeSpawnService;
        private readonly SplitScreenCameraSetup _cameraSetup;
        private readonly PresentationInitializer _presentationInit;
        private readonly InputInitializer _inputInit;
        private readonly MatchClock _clock;
        private readonly IAudioService _audio;

        // Dispose 用: フェーズ SE 購読の解除
        private System.IDisposable _phaseSub;

        public MatchInitializer(
            IBalanceParameters balance,
            IRandomProvider random,
            StageModel stage,
            WallGenerationService wallGen,
            MatchPlayers players,
            SlimeSpawnService slimeSpawnService,
            SplitScreenCameraSetup cameraSetup,
            PresentationInitializer presentationInit,
            InputInitializer inputInit,
            MatchClock clock,
            IAudioService audio)
        {
            _balance = balance;
            _random = random;
            _stage = stage;
            _wallGen = wallGen;
            _players = players;
            _slimeSpawnService = slimeSpawnService;
            _cameraSetup = cameraSetup;
            _presentationInit = presentationInit;
            _inputInit = inputInit;
            _clock = clock;
            _audio = audio;
        }

        public async UniTask StartAsync(CancellationToken ct)
        {
            // 0. BGM 切替 (タイトル BGM 停止 → ゲーム BGM 開始)
            _audio?.StopBgm(0.3f);
            _audio?.PlayBgm(SfxIds.BgmMatch);

            // 1. 壁生成 (Domain)
            var bounds = _stage.GetCurrentBounds();
            var p1Spawn = _players.Player1.CurrentPosition;
            var p2Spawn = _players.Player2.CurrentPosition;
            var walls = _wallGen.Generate(bounds, p1Spawn, p2Spawn, _random);
            foreach (var pos in walls)
                _stage.SetTileState(pos, TileState.Wall);

            // 2. Presentation 初期化 (TileView → Presenter → HUD → Overlay → Result)
            _presentationInit.Initialize();

            // 3. カメラセットアップ
            _cameraSetup.Initialize(_players.Player1, _players.Player2, _stage.Bounds);

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
