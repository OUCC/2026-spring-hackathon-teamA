using System.Collections.Generic;
using UnityEngine;
using FloorBreaker.Shared.Domain.Grid;
using FloorBreaker.Shared.Domain.Primitives;
using FloorBreaker.Shared.Application.Interfaces;
using FloorBreaker.Shared.Infrastructure.Random;
using FloorBreaker.Shared.Presentation.Common;
using FloorBreaker.Stage.Domain;
using FloorBreaker.Stage.Presentation;
using FloorBreaker.Player.Domain;
using FloorBreaker.Player.Application;
using FloorBreaker.Player.Presentation;
using FloorBreaker.Bombs.Domain;
using FloorBreaker.Bombs.Application;
using FloorBreaker.MatchFlow.Application;
using FloorBreaker.Slimes.Domain;
using FloorBreaker.Slimes.Application;
using FloorBreaker.ScriptableObjects.Balance;
using FloorBreaker.Shared.Infrastructure.Audio;

namespace FloorBreaker.Slimes.Presentation.Debug
{
    /// <summary>
    /// スライム Presentation プレビュー用コントローラー。
    /// キーボードでスライムのスポーン・移動・死亡を目視確認する。
    /// DI なし、手動配線。
    /// </summary>
    public sealed class SlimePreviewController : MonoBehaviour
    {
        [Header("Factories (シーンに配置)")]
        [SerializeField] private StageViewFactory _stageFactory;
        [SerializeField] private PlayerViewFactory _playerFactory;
        [SerializeField] private SlimeViewFactory _slimeFactory;

        [Header("VFX")]
        [SerializeField] private GameObject _fireVfxPrefab;

        [Header("Settings")]
        [SerializeField] private int _stageSize = 30;
        [SerializeField] private BalanceConfig _balance;

        // Domain — Stage
        private StageModel _stageModel;
        private TileTimerService _tileTimerService;
        private SafeTileSearchService _safeTileSearch;
        private PlayerDamageService _damageService;
        private PlayerMoveService _moveService;

        // Domain — Player
        private PlayerModel _player1;
        private PlayerModel _player2;

        // Domain — Slime
        private SlimeRegistry _slimeRegistry;
        private SlimeSpawnService _slimeSpawnService;
        private SlimeAiService _slimeAiService;
        private SlimeTickService _slimeTickService;

        // Bomb simulation
        private StageQueryService _queryService;
        private BombAreaResolver _areaResolver;
        private BreakBombResolver _breakResolver;
        private FireBombResolver _fireResolver;
        private BombEffectSpreadService _spreadService;
        private FireDamageTickService _fireDamageTickService;
        private IRandomProvider _random;

        // Presentation — Stage
        private Dictionary<GridPos, TileView> _tileViews;
        private StagePresenter _stagePresenter;
        private TileAnimationService _tileAnimService;
        private TileFireVfxPool _fireVfxPool;

        // Presentation — Player
        private PlayerView _p1View;
        private PlayerView _p2View;
        private PlayerPresenter _p1Presenter;
        private PlayerPresenter _p2Presenter;
        private PlayerAnimationService _playerAnimService;

        // Presentation — Slime
        private SlimeAnimationService _slimeAnimService;
        private SlimePresenter _slimePresenter;

        // Input state
        private float _p1MoveTimer;
        private float _p2MoveTimer;
        private const float MoveRepeatRate = 0.12f;
        private const float MoveFirstDelay = 0.01f;

        // Random bomb state
        private float _randomBombTimer;
        private const float RandomBombInterval = 1.5f;
        private const int BombEffectRange = 2;
        private const float FireSpreadInterval = 0.15f;
        private const float BreakSpreadInterval = 0.3f;

        // Auto-spawn toggle
        private bool _autoSpawnEnabled = true;

        // Balance defaults
        private const float InvulnerabilityDuration = 1.5f;
        private const float ForcedMoveDuration = 1f;

        private void Start()
        {
            SetupStage();
            SetupPlayers();
            SetupSlimes();
            SetupCamera();
            LogControls();
        }

        private void SetupStage()
        {
            var bounds = new TileCoordRange(0, 0, _stageSize - 1, _stageSize - 1);
            _stageModel = new StageModel(bounds);
            _tileTimerService = new TileTimerService(_stageModel);
            _safeTileSearch = new SafeTileSearchService();
            _damageService = new PlayerDamageService(InvulnerabilityDuration, ForcedMoveDuration, _stageModel, _safeTileSearch);

            // 壁生成
            IRandomProvider random = new SeededRandomProvider(42);
            var wallGenService = new WallGenerationService(0.08f, 0.40f, 0.20f, 2);
            var p1Spawn = new GridPos(2, 2);
            var p2Spawn = new GridPos(_stageSize - 3, _stageSize - 3);
            var walls = wallGenService.Generate(bounds, p1Spawn, p2Spawn, random);
            foreach (var pos in walls)
            {
                _stageModel.SetTileState(pos, TileState.Wall);
            }

            // Bomb simulation
            _random = new SeededRandomProvider(123);
            _queryService = new StageQueryService(_stageModel);
            _areaResolver = new BombAreaResolver(_queryService);
            _breakResolver = new BreakBombResolver(_areaResolver);
            _fireResolver = new FireBombResolver(_areaResolver);

            // Stage Presentation
            var config = _stageFactory.Config;
            _tileViews = _stageFactory.CreateTileViews(_stageModel, bounds);
            _tileAnimService = new TileAnimationService(config);

            var vfxParent = new GameObject("VfxPool").transform;
            vfxParent.SetParent(transform, false);
            var vfxPrefab = _fireVfxPrefab != null ? _fireVfxPrefab : config.FireVfxPrefab;
            _fireVfxPool = new TileFireVfxPool(vfxPrefab, vfxParent);

            var nullAudio = new NullAudioService();
            _stagePresenter = new StagePresenter(
                _stageModel, _tileViews, _tileAnimService, _fireVfxPool, config, nullAudio);
        }

        private void SetupPlayers()
        {
            _moveService = new PlayerMoveService();

            var p1Spawn = new GridPos(2, 2);
            var p2Spawn = new GridPos(_stageSize - 3, _stageSize - 3);

            _player1 = CreatePlayerModel(PlayerId.Player1, p1Spawn);
            _player2 = CreatePlayerModel(PlayerId.Player2, p2Spawn);

            var playerConfig = _playerFactory.Config;
            _playerAnimService = new PlayerAnimationService(playerConfig);

            _p1View = _playerFactory.CreatePlayerView(PlayerId.Player1, p1Spawn);
            _p2View = _playerFactory.CreatePlayerView(PlayerId.Player2, p2Spawn);

            var nullAudio = new NullAudioService();
            var nullCameraShake = new NullCameraShakeService();
            var nullImpactFreeze = new NullImpactFreezeService();
            _p1Presenter = new PlayerPresenter(_player1, _p1View, _playerAnimService, playerConfig, nullAudio, nullCameraShake, nullImpactFreeze);
            _p2Presenter = new PlayerPresenter(_player2, _p2View, _playerAnimService, playerConfig, nullAudio, nullCameraShake, nullImpactFreeze);
        }

        private PlayerModel CreatePlayerModel(PlayerId id, GridPos spawn)
        {
            var stats = new PlayerStats(10, 1f, 3f);
            var build = new PlayerBuild(3, 1, 1, 2f, 3.5f, false, 0.5f, 3, 1, 2, 4f, 3f, 1f);
            return new PlayerModel(id, spawn, stats, build);
        }

        private void SetupSlimes()
        {
            _slimeRegistry = new SlimeRegistry();
            var players = new List<PlayerModel> { _player1, _player2 };
            _slimeSpawnService = new SlimeSpawnService(_stageModel, _slimeRegistry, players, _random, _balance);
            _slimeAiService = new SlimeAiService(_damageService, _safeTileSearch, _slimeRegistry, players, _stageModel, _balance);
            _slimeTickService = new SlimeTickService(
                _slimeAiService, _slimeSpawnService, _slimeRegistry, _tileTimerService, _balance.SlimeSpawnCheckInterval);

            // BombEffectSpreadService にスライムレジストリを渡す（ボムでスライム撃破テスト用）
            _spreadService = new BombEffectSpreadService(
                _stageModel, _tileTimerService, _damageService, _safeTileSearch,
                _slimeRegistry);
            _fireDamageTickService = new FireDamageTickService(
                _damageService, _safeTileSearch, _slimeRegistry, _balance);

            // Slime Presentation
            var slimeConfig = _slimeFactory.Config;
            _slimeAnimService = new SlimeAnimationService(slimeConfig);
            var nullAudioSlime = new NullAudioService();
            var nullCameraShakeSlime = new NullCameraShakeService();
            _slimePresenter = new SlimePresenter(
                _slimeRegistry, _slimeFactory, _slimeAnimService, slimeConfig, nullAudioSlime, nullCameraShakeSlime);
        }

        private void SetupCamera()
        {
            var cam = Camera.main;
            if (cam != null)
            {
                cam.orthographic = true;
                cam.orthographicSize = _stageSize * 0.55f;
                float center = _stageSize * 0.5f;
                cam.transform.position = new Vector3(center, center, -10f);
            }
        }

        private void Update()
        {
            float dt = Time.deltaTime;

            // Domain ticks
            _tileTimerService.Tick(dt);
            _spreadService.Tick(dt);
            _player1.Invulnerability.Tick(dt);
            _player2.Invulnerability.Tick(dt);
            _player1.ForcedMove.Tick(dt);
            _player2.ForcedMove.Tick(dt);

            // Fire DoT
            var players = new List<PlayerModel> { _player1, _player2 };
            _fireDamageTickService.Tick(dt, players, _stageModel);

            // Slime AI + spawn
            if (_autoSpawnEnabled)
            {
                _slimeTickService.Tick(dt);
            }
            else
            {
                // AI のみ Tick (スポーンなし)
                _slimeAiService.TickAll(dt);
            }

            // Random bomb timer
            _randomBombTimer -= dt;
            if (_randomBombTimer <= 0f)
            {
                _randomBombTimer = RandomBombInterval;
                SpawnRandomBomb();
            }

            // Presenter ticks
            _p1Presenter.Tick(dt);
            _p2Presenter.Tick(dt);

            // Player input
            HandleP1Input(dt);
            HandleP2Input(dt);

            // Action keys
            HandleActionKeys();
        }

        // ─── P1 Input: WASD ─────────────────────────────────────

        private void HandleP1Input(float dt)
        {
            var dir = ReadWASD();
            if (dir.HasValue)
            {
                _p1MoveTimer -= dt;
                if (_p1MoveTimer <= 0f)
                {
                    _moveService.TryMove(_player1, dir.Value, _stageModel);
                    _p1MoveTimer = MoveRepeatRate;
                }
            }
            else
            {
                _p1MoveTimer = MoveFirstDelay;
            }
        }

        private Direction8? ReadWASD()
        {
            bool w = Input.GetKey(KeyCode.W);
            bool a = Input.GetKey(KeyCode.A);
            bool s = Input.GetKey(KeyCode.S);
            bool d = Input.GetKey(KeyCode.D);

            if (w && d) return Direction8.NE;
            if (w && a) return Direction8.NW;
            if (s && d) return Direction8.SE;
            if (s && a) return Direction8.SW;
            if (w) return Direction8.N;
            if (s) return Direction8.S;
            if (a) return Direction8.W;
            if (d) return Direction8.E;
            return null;
        }

        // ─── P2 Input: Arrow Keys ───────────────────────────────

        private void HandleP2Input(float dt)
        {
            var dir = ReadArrows();
            if (dir.HasValue)
            {
                _p2MoveTimer -= dt;
                if (_p2MoveTimer <= 0f)
                {
                    _moveService.TryMove(_player2, dir.Value, _stageModel);
                    _p2MoveTimer = MoveRepeatRate;
                }
            }
            else
            {
                _p2MoveTimer = MoveFirstDelay;
            }
        }

        private Direction8? ReadArrows()
        {
            bool up = Input.GetKey(KeyCode.UpArrow);
            bool down = Input.GetKey(KeyCode.DownArrow);
            bool left = Input.GetKey(KeyCode.LeftArrow);
            bool right = Input.GetKey(KeyCode.RightArrow);

            if (up && right) return Direction8.NE;
            if (up && left) return Direction8.NW;
            if (down && right) return Direction8.SE;
            if (down && left) return Direction8.SW;
            if (up) return Direction8.N;
            if (down) return Direction8.S;
            if (left) return Direction8.W;
            if (right) return Direction8.E;
            return null;
        }

        // ─── Action Keys ────────────────────────────────────────

        private void HandleActionKeys()
        {
            if (Input.GetKeyDown(KeyCode.T)) SpawnSlimeManual(SlimeType.Normal);
            if (Input.GetKeyDown(KeyCode.Y)) SpawnSlimeManual(SlimeType.Gold);
            if (Input.GetKeyDown(KeyCode.U)) SpawnSlimeManual(SlimeType.Red);
            if (Input.GetKeyDown(KeyCode.N)) KillNearestSlime();
            if (Input.GetKeyDown(KeyCode.M)) ToggleAutoSpawn();
            if (Input.GetKeyDown(KeyCode.H)) DamagePlayer(_player1, 1);
            if (Input.GetKeyDown(KeyCode.J)) DamagePlayer(_player2, 1);
            if (Input.GetKeyDown(KeyCode.R)) ResetAll();
        }

        private void SpawnSlimeManual(SlimeType type)
        {
            var bounds = _stageModel.GetCurrentBounds();
            for (int i = 0; i < 20; i++)
            {
                int x = _random.Range(bounds.MinX, bounds.MaxX + 1);
                int y = _random.Range(bounds.MinY, bounds.MaxY + 1);
                var pos = new GridPos(x, y);
                if (_stageModel.IsPassable(pos) && !_slimeRegistry.IsOccupied(pos))
                {
                    var slime = new SlimeModel(SlimeId.Next(), type, pos, 1f);
                    _slimeRegistry.Add(slime);
                    UnityEngine.Debug.Log($"[SlimePreview] {type} スライムを {pos} にスポーン");
                    return;
                }
            }
            UnityEngine.Debug.Log("[SlimePreview] スポーン可能なマスが見つかりませんでした");
        }

        private void KillNearestSlime()
        {
            var center = _player1.CurrentPosition;
            SlimeModel nearest = null;
            int minDist = int.MaxValue;

            foreach (var slime in _slimeRegistry.GetAll())
            {
                if (!slime.IsAlive) continue;
                int dist = slime.Position.ChebyshevDistance(center);
                if (dist < minDist)
                {
                    minDist = dist;
                    nearest = slime;
                }
            }

            if (nearest != null)
            {
                nearest.Kill();
                _slimeRegistry.Remove(nearest.Id);
                UnityEngine.Debug.Log($"[SlimePreview] {nearest.Type} スライム (ID:{nearest.Id}) を撃破");
            }
            else
            {
                UnityEngine.Debug.Log("[SlimePreview] 撃破対象のスライムがいません");
            }
        }

        private void ToggleAutoSpawn()
        {
            _autoSpawnEnabled = !_autoSpawnEnabled;
            UnityEngine.Debug.Log($"[SlimePreview] 自動スポーン: {(_autoSpawnEnabled ? "ON" : "OFF")}");
        }

        private void DamagePlayer(PlayerModel player, int amount)
        {
            var occupied = new HashSet<GridPos> { _player1.CurrentPosition, _player2.CurrentPosition };
            _damageService.ApplyDamage(player, amount, false, occupied);
            UnityEngine.Debug.Log($"[SlimePreview] {player.Id} にダメージ {amount} → HP {player.Stats.CurrentHp.CurrentValue}");
        }

        private void SpawnRandomBomb()
        {
            var bounds = _stageModel.GetCurrentBounds();
            GridPos center = default;
            bool found = false;
            for (int i = 0; i < 10; i++)
            {
                int x = _random.Range(bounds.MinX, bounds.MaxX + 1);
                int y = _random.Range(bounds.MinY, bounds.MaxY + 1);
                var pos = new GridPos(x, y);
                if (_stageModel.IsPassable(pos))
                {
                    center = pos;
                    found = true;
                    break;
                }
            }
            if (!found) return;

            var players = new List<PlayerModel> { _player1, _player2 };
            bool isFire = _random.Range(0, 2) == 0;

            if (isFire)
            {
                var spec = new BombSpec(BombType.Fire, 3, 3, BombEffectRange, 1, 2f, false, 3.5f, 0f, 0f);
                var result = _fireResolver.Resolve(center, spec, _stageModel);
                _spreadService.EnqueueFireBomb(result, center, players, null, FireSpreadInterval);
            }
            else
            {
                var spec = new BombSpec(BombType.Break, 3, 3, BombEffectRange, 2, 4f, true, 0f, 3f, 5f);
                var result = _breakResolver.Resolve(center, spec, _stageModel);
                _spreadService.EnqueueBreakBomb(result, center, players, null, BreakSpreadInterval);
            }
        }

        private void ResetAll()
        {
            OnDestroy();
            Start();
            UnityEngine.Debug.Log("[SlimePreview] リセット完了");
        }

        private void LogControls()
        {
            UnityEngine.Debug.Log("[SlimePreview] 操作ガイド:");
            UnityEngine.Debug.Log("  W/A/S/D: P1 移動  |  矢印キー: P2 移動");
            UnityEngine.Debug.Log("  T: ノーマルスライム  |  Y: 金色スライム  |  U: 赤色スライム");
            UnityEngine.Debug.Log("  N: 最寄りスライム撃破  |  M: 自動スポーン ON/OFF");
            UnityEngine.Debug.Log("  H: P1 ダメージ  |  J: P2 ダメージ  |  R: 全リセット");
            UnityEngine.Debug.Log("  ※ 1.5秒ごとにランダムボム (範囲2) が自動発生します");
        }

        private void OnDestroy()
        {
            _slimePresenter?.Dispose();
            _slimeAnimService?.Dispose();
            _slimeTickService?.Dispose();
            _slimeRegistry?.Dispose();
            _p1Presenter?.Dispose();
            _p2Presenter?.Dispose();
            _playerAnimService?.Dispose();
            _stagePresenter?.Dispose();
            _tileAnimService?.Dispose();
            _fireVfxPool?.Dispose();
            _tileTimerService?.Dispose();
            _player1?.Dispose();
            _player2?.Dispose();
            _stageModel?.Dispose();
        }
    }
}
