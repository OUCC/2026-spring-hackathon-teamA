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
using FloorBreaker.Bombs.Domain;
using FloorBreaker.Bombs.Application;
using FloorBreaker.MatchFlow.Application;
using FloorBreaker.ScriptableObjects.Balance;
using FloorBreaker.Shared.Infrastructure.Audio;

namespace FloorBreaker.Player.Presentation.Debug
{
    /// <summary>
    /// プレイヤー Presentation プレビュー用コントローラー。
    /// キーボードで P1/P2 を操作し、移動・ダメージ・強制移動・死亡の演出を目視確認する。
    /// DI なし、手動配線。
    /// </summary>
    public sealed class PlayerPreviewController : MonoBehaviour
    {
        [Header("Factories (シーンに配置)")]
        [SerializeField] private StageViewFactory _stageFactory;
        [SerializeField] private PlayerViewFactory _playerFactory;

        [Header("VFX")]
        [SerializeField] private GameObject _fireVfxPrefab;

        [Header("Settings")]
        [SerializeField] private int _stageSize = 30;
        [SerializeField] private BalanceConfig _balance;

        // Domain
        private StageModel _stageModel;
        private TileTimerService _tileTimerService;
        private WallGenerationService _wallGenService;
        private SafeTileSearchService _safeTileSearch;
        private PlayerMoveService _moveService;
        private PlayerDamageService _damageService;
        private PlayerModel _player1;
        private PlayerModel _player2;

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

        // Input state
        private float _p1MoveTimer;
        private float _p2MoveTimer;
        private const float MoveRepeatRate = 0.12f;
        private const float MoveFirstDelay = 0.01f; // 初回は即移動

        // Random bomb state
        private float _randomBombTimer;
        private const float RandomBombInterval = 1.5f;
        private const int BombEffectRange = 10;
        private const float FireSpreadInterval = 0.15f;
        private const float BreakSpreadInterval = 0.3f;

        // Balance defaults
        private const float InvulnerabilityDuration = 1.5f;
        private const float ForcedMoveDuration = 1f;

        private void Start()
        {
            SetupStage();
            SetupPlayers();
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
            _wallGenService = new WallGenerationService(0.08f, 0.40f, 0.20f, 2);
            var p1Spawn = new GridPos(2, 2);
            var p2Spawn = new GridPos(_stageSize - 3, _stageSize - 3);
            var walls = _wallGenService.Generate(bounds, p1Spawn, p2Spawn, random);
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
            _spreadService = new BombEffectSpreadService(
                _stageModel, _tileTimerService, _damageService, _safeTileSearch);
            _fireDamageTickService = new FireDamageTickService(
                _damageService, _safeTileSearch, null, _balance);

            // Stage Presentation
            var config = _stageFactory.Config;
            _tileViews = _stageFactory.CreateTileViews(_stageModel, bounds);
            _tileAnimService = new TileAnimationService(config);

            var vfxParent = new GameObject("VfxPool").transform;
            vfxParent.SetParent(transform, false);
            // _fireVfxPrefab が未設定なら TileSpriteConfig から取得
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

            // Random bomb timer
            _randomBombTimer -= dt;
            if (_randomBombTimer <= 0f)
            {
                _randomBombTimer = RandomBombInterval;
                SpawnRandomBomb();
            }

            // Presenter ticks (walk frame, invulnerability visual)
            _p1Presenter.Tick(dt);
            _p2Presenter.Tick(dt);

            // Player input
            HandleP1Input(dt);
            HandleP2Input(dt);

            // Action keys
            if (Input.GetKeyDown(KeyCode.H)) DamagePlayer(_player1, 1);
            if (Input.GetKeyDown(KeyCode.J)) DamagePlayer(_player2, 1);
            if (Input.GetKeyDown(KeyCode.G)) ForceRelocate(_player1);
            if (Input.GetKeyDown(KeyCode.K)) DamagePlayer(_player1, _player1.Stats.CurrentHp.CurrentValue);
            if (Input.GetKeyDown(KeyCode.L)) DamagePlayer(_player2, _player2.Stats.CurrentHp.CurrentValue);
            if (Input.GetKeyDown(KeyCode.R)) ResetAll();
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

        // ─── Actions ────────────────────────────────────────────

        private void DamagePlayer(PlayerModel player, int amount)
        {
            var occupied = new HashSet<GridPos> { _player1.CurrentPosition, _player2.CurrentPosition };
            _damageService.ApplyDamage(player, amount, false, occupied);
            UnityEngine.Debug.Log($"[PlayerPreview] {player.Id} にダメージ {amount} → HP {player.Stats.CurrentHp.CurrentValue}");
        }

        private void ForceRelocate(PlayerModel player)
        {
            var occupied = new HashSet<GridPos> { _player1.CurrentPosition, _player2.CurrentPosition };
            _damageService.ApplyDamage(player, 2, true, occupied);
            UnityEngine.Debug.Log($"[PlayerPreview] {player.Id} を強制移動 → {player.CurrentPosition}, HP {player.Stats.CurrentHp.CurrentValue}");
        }

        private void SpawnRandomBomb()
        {
            var bounds = _stageModel.GetCurrentBounds();
            // ランダム位置を bounds 内で直接生成
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

            // 50/50 で炎 or ブレーク
            bool isFire = _random.Range(0, 2) == 0;
            var players = new List<PlayerModel> { _player1, _player2 };

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
            // Cleanup old
            _p1Presenter?.Dispose();
            _p2Presenter?.Dispose();
            _playerAnimService?.Dispose();
            _player1?.Dispose();
            _player2?.Dispose();
            if (_p1View != null) Destroy(_p1View.gameObject);
            if (_p2View != null) Destroy(_p2View.gameObject);

            // Recreate players
            SetupPlayers();
            UnityEngine.Debug.Log("[PlayerPreview] リセット完了");
        }

        private void LogControls()
        {
            UnityEngine.Debug.Log("[PlayerPreview] 操作ガイド:");
            UnityEngine.Debug.Log("  W/A/S/D: P1 移動 (同時押しで斜め)");
            UnityEngine.Debug.Log("  矢印キー: P2 移動 (同時押しで斜め)");
            UnityEngine.Debug.Log("  H: P1 ダメージ  |  J: P2 ダメージ");
            UnityEngine.Debug.Log("  G: P1 強制移動  |  K: P1 即死  |  L: P2 即死");
            UnityEngine.Debug.Log("  R: 全リセット");
            UnityEngine.Debug.Log("  ※ 1.5秒ごとにランダムボム (範囲10) が自動発生します");
        }

        private void OnDestroy()
        {
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
