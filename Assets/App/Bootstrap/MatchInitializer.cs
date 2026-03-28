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
        private readonly WallGenerationService _wallGen;
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
            WallGenerationService wallGen,
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
            _wallGen = wallGen;
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

            // 1. 壁生成 (Domain)
            var bounds = _stage.GetCurrentBounds();
            var spawnPositions = new System.Collections.Generic.List<FloorBreaker.Shared.Domain.Grid.GridPos>();
            foreach (var p in _players.All) spawnPositions.Add(p.CurrentPosition);
            var walls = _wallGen.Generate(bounds, spawnPositions, _random);
            foreach (var pos in walls)
                _stage.SetTileData(pos, new TileData
                {
                    Type = TileType.Wall,
                    Condition = TileCondition.Intact,
                    WarpPairId = -1,
                });

            // 1b. ガス管ランダム生成 (StageConfig.GasVeinCount > 0 の場合)
            if (_stageConfig != null && _stageConfig.GasVeinCount > 0)
            {
                GenerateGasVeins(bounds);
            }

            // 1c. プリセットタイル配置 (StageConfig)
            if (_stageConfig?.PresetTiles != null)
            {
                foreach (var preset in _stageConfig.PresetTiles)
                {
                    var presetPos = new FloorBreaker.Shared.Domain.Grid.GridPos(preset.x, preset.y);
                    if (!_stage.IsInBounds(presetPos)) continue;
                    _stage.SetTileData(presetPos, new TileData
                    {
                        Type = preset.type,
                        Condition = preset.condition,
                        WarpPairId = preset.warpPairId,
                    });
                }
            }

            // 1d. WarpService レジストリ構築
            _warpService?.BuildRegistry(bounds);

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

        /// <summary>
        /// ガス管をランダムウォークで生成する。各 vein は seed 地点から
        /// ランダムな4方向に伸びる細い連結線。
        /// </summary>
        private void GenerateGasVeins(FloorBreaker.Shared.Domain.Grid.TileCoordRange bounds)
        {
            int veinCount = _stageConfig.GasVeinCount;
            int minLen = _stageConfig.GasVeinMinLength;
            int maxLen = _stageConfig.GasVeinMaxLength;
            int protect = _balance.SpawnProtectionRadius;

            var spawnPositions = new System.Collections.Generic.List<FloorBreaker.Shared.Domain.Grid.GridPos>();
            foreach (var p in _players.All) spawnPositions.Add(p.CurrentPosition);

            // 4方向
            var dirs = new (int dx, int dy)[] { (1, 0), (-1, 0), (0, 1), (0, -1) };

            for (int v = 0; v < veinCount; v++)
            {
                // ランダム seed 位置
                int attempts = 0;
                int sx, sy;
                do
                {
                    sx = _random.Range(bounds.MinX + 3, bounds.MaxX - 2);
                    sy = _random.Range(bounds.MinY + 3, bounds.MaxY - 2);
                    attempts++;
                } while (attempts < 50 && IsNearSpawn(sx, sy, spawnPositions, protect));

                if (attempts >= 50) continue;

                // ランダムウォークで vein を伸ばす
                int length = _random.Range(minLen, maxLen + 1);
                int cx = sx, cy = sy;
                var dir = dirs[_random.Range(0, 4)];

                for (int step = 0; step < length; step++)
                {
                    var pos = new FloorBreaker.Shared.Domain.Grid.GridPos(cx, cy);
                    if (!_stage.IsInBounds(pos)) break;

                    var existing = _stage.GetTileData(pos);
                    // 壁・岩盤・既にガスの場合はスキップ
                    if (existing.Type == TileType.Wall || existing.Type == TileType.Bedrock)
                        break;
                    if (existing.Type != TileType.Gas && existing.Condition == TileCondition.Intact)
                    {
                        _stage.SetTileData(pos, new TileData
                        {
                            Type = TileType.Gas,
                            Condition = TileCondition.Intact,
                            WarpPairId = -1,
                        });
                    }

                    // 次のステップ: 70% で同方向、30% で直角に曲がる
                    if (_random.Range(0, 100) < 30)
                    {
                        // 直角方向に変更
                        if (dir.dx != 0)
                            dir = _random.Range(0, 2) == 0 ? (0, 1) : (0, -1);
                        else
                            dir = _random.Range(0, 2) == 0 ? (1, 0) : (-1, 0);
                    }

                    cx += dir.dx;
                    cy += dir.dy;
                }
            }
        }

        private static bool IsNearSpawn(int x, int y,
            System.Collections.Generic.List<FloorBreaker.Shared.Domain.Grid.GridPos> spawns, int radius)
        {
            foreach (var s in spawns)
            {
                if (System.Math.Abs(x - s.X) <= radius && System.Math.Abs(y - s.Y) <= radius)
                    return true;
            }
            return false;
        }

        public void Dispose()
        {
            _phaseSub?.Dispose();
            _inputInit?.Dispose();
        }
    }
}
