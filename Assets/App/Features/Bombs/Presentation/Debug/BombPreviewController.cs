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
using FloorBreaker.Player.Presentation;
using FloorBreaker.Bombs.Domain;
using FloorBreaker.Bombs.Application;
using FloorBreaker.MatchFlow.Application;
using FloorBreaker.Slimes.Domain;
using FloorBreaker.ScriptableObjects.Balance;

namespace FloorBreaker.Bombs.Presentation.Debug
{
    /// <summary>
    /// ボム Presentation プレビュー用コントローラー。
    /// ホールド/リリースでボム飛行→着弾の演出を目視確認する。
    /// DI なし、手動配線。PlayerPreviewController パターン踏襲。
    /// </summary>
    public sealed class BombPreviewController : MonoBehaviour
    {
        [Header("Factories (シーンに配置)")]
        [SerializeField] private StageViewFactory _stageFactory;
        [SerializeField] private PlayerViewFactory _playerFactory;
        [SerializeField] private BombViewFactory _bombFactory;

        [Header("VFX")]
        [SerializeField] private GameObject _fireVfxPrefab;

        [Header("Settings")]
        [SerializeField] private int _stageSize = 30;
        [SerializeField] private BalanceConfig _balance;

        // Domain
        private StageModel _stageModel;
        private TileTimerService _tileTimerService;
        private WallGenerationService _wallGenService;
        private StageQueryService _queryService;
        private SafeTileSearchService _safeTileSearch;
        private PlayerMoveService _moveService;
        private PlayerDamageService _damageService;
        private FireDamageTickService _fireDamageTickService;
        private PlayerModel _player1;
        private PlayerModel _player2;
        private List<PlayerModel> _players;

        // Bombs
        private BombCooldownState _p1Cooldown;
        private BombCooldownState _p2Cooldown;
        private BombLaunchUseCase _launchUseCase;
        private BombFlightTracker _tracker;
        private BombEffectSpreadService _spreadService;
        private SlimeRegistry _slimeRegistry;

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

        // Presentation — Bomb
        private BombPresenter _bombPresenter;
        private BombAnimationService _bombAnimService;
        private BombExplosionVfxPool _bombVfxPool;

        // Input state
        private float _p1MoveTimer;
        private float _p2MoveTimer;
        private const float MoveRepeatRate = 0.12f;
        private const float MoveFirstDelay = 0.01f;
        private int _effectRange = 1;
        private int _maxFlightOverride = 3;
        private bool _initialized;

        private void Start()
        {
            if (_stageFactory == null || _playerFactory == null || _bombFactory == null
                || _balance == null || _fireVfxPrefab == null)
            {
                UnityEngine.Debug.LogError(
                    "[BombPreview] SerializeField が未設定です。Inspector で全て割り当ててください。" +
                    $" StageFactory={_stageFactory != null}, PlayerFactory={_playerFactory != null}," +
                    $" BombFactory={_bombFactory != null}, Balance={_balance != null}," +
                    $" FireVfxPrefab={_fireVfxPrefab != null}");
                return;
            }

            SetupDomain();
            SetupStagePresentation();
            SetupPlayers();
            SetupBombs();
            SetupBombPresentation();
            SetupCamera();
            _initialized = true;
            LogControls();
        }

        private void SetupDomain()
        {
            var bounds = new TileCoordRange(0, 0, _stageSize - 1, _stageSize - 1);
            _stageModel = new StageModel(bounds);
            _tileTimerService = new TileTimerService(_stageModel);
            _safeTileSearch = new SafeTileSearchService();
            _damageService = new PlayerDamageService(
                _balance.InvulnerabilityDuration, _balance.ForcedMoveDuration);
            _queryService = new StageQueryService(_stageModel);
            _slimeRegistry = new SlimeRegistry();

            // 壁生成
            IRandomProvider random = new SeededRandomProvider(42);
            _wallGenService = new WallGenerationService(
                _balance.WallSeedPercent, _balance.WallGrowthChance,
                _balance.WallTargetPercent, _balance.SpawnProtectionRadius);
            var p1Spawn = new GridPos(
                _balance.SpawnProtectionRadius, _balance.SpawnProtectionRadius);
            var p2Spawn = new GridPos(
                _stageSize - 1 - _balance.SpawnProtectionRadius,
                _stageSize - 1 - _balance.SpawnProtectionRadius);
            var walls = _wallGenService.Generate(bounds, p1Spawn, p2Spawn, random);
            foreach (var pos in walls)
            {
                _stageModel.SetTileState(pos, TileState.Wall);
            }

            _fireDamageTickService = new FireDamageTickService(
                _damageService, _safeTileSearch, null, _balance);
        }

        private void SetupStagePresentation()
        {
            var bounds = _stageModel.GetCurrentBounds();
            var config = _stageFactory.Config;
            _tileViews = _stageFactory.CreateTileViews(_stageModel, bounds);
            _tileAnimService = new TileAnimationService(config);

            var vfxParent = new GameObject("TileVfxPool").transform;
            vfxParent.SetParent(transform, false);
            _fireVfxPool = new TileFireVfxPool(_fireVfxPrefab, vfxParent);

            _stagePresenter = new StagePresenter(
                _stageModel, _tileViews, _tileAnimService, _fireVfxPool, config);
        }

        private void SetupPlayers()
        {
            _moveService = new PlayerMoveService();

            var p1Spawn = new GridPos(
                _balance.SpawnProtectionRadius, _balance.SpawnProtectionRadius);
            var p2Spawn = new GridPos(
                _stageSize - 1 - _balance.SpawnProtectionRadius,
                _stageSize - 1 - _balance.SpawnProtectionRadius);

            _player1 = CreatePlayerModel(PlayerId.Player1, p1Spawn);
            _player2 = CreatePlayerModel(PlayerId.Player2, p2Spawn);
            _players = new List<PlayerModel> { _player1, _player2 };

            var playerConfig = _playerFactory.Config;
            _playerAnimService = new PlayerAnimationService(playerConfig);

            _p1View = _playerFactory.CreatePlayerView(PlayerId.Player1, p1Spawn);
            _p2View = _playerFactory.CreatePlayerView(PlayerId.Player2, p2Spawn);

            _p1Presenter = new PlayerPresenter(
                _player1, _p1View, _playerAnimService, playerConfig);
            _p2Presenter = new PlayerPresenter(
                _player2, _p2View, _playerAnimService, playerConfig);
        }

        private void SetupBombs()
        {
            _p1Cooldown = new BombCooldownState();
            _p2Cooldown = new BombCooldownState();

            var areaResolver = new BombAreaResolver(_queryService);
            var landingResolver = new BombLandingResolver(_stageModel);
            var breakResolver = new BreakBombResolver(areaResolver);
            var fireResolver = new FireBombResolver(areaResolver);

            _spreadService = new BombEffectSpreadService(
                _stageModel, _tileTimerService, _damageService, _safeTileSearch,
                _slimeRegistry);

            _launchUseCase = new BombLaunchUseCase(
                landingResolver, breakResolver, fireResolver,
                _stageModel, _balance, _spreadService);

            _tracker = new BombFlightTracker(
                _launchUseCase, _p1Cooldown, _p2Cooldown,
                _stageModel, _slimeRegistry, _balance);
        }

        private void SetupBombPresentation()
        {
            var bombConfig = _bombFactory.Config;
            _bombAnimService = new BombAnimationService(bombConfig);

            var vfxParent = new GameObject("BombVfxPool").transform;
            vfxParent.SetParent(transform, false);
            _bombVfxPool = new BombExplosionVfxPool(
                bombConfig.GetExplosionPrefab(BombType.Fire),
                bombConfig.GetExplosionPrefab(BombType.Break),
                vfxParent, bombConfig.ExplosionVfxScale, bombConfig.ExplosionVfxDuration);

            _bombPresenter = new BombPresenter(
                _tracker, _bombFactory, _bombAnimService, _bombVfxPool,
                bombConfig, _queryService, _tileViews, _balance.BombFlightSpeed);
        }

        private PlayerModel CreatePlayerModel(PlayerId id, GridPos spawn)
        {
            var stats = new PlayerStats(
                _balance.InitialHp, _balance.BaseMovementSpeed, _balance.MaxMovementSpeed);
            var build = new PlayerBuild(
                _balance.FireBombMaxFlightDistance, _balance.FireBombEffectRange,
                _balance.FireBombContactDamage, _balance.FireBombCooldown,
                _balance.FireBombDuration, _balance.FireBombDefaultWallPenetration,
                _balance.FireBombCooldownMin,
                _balance.BreakBombMaxFlightDistance, _balance.BreakBombEffectRange,
                _balance.BreakBombDamage, _balance.BreakBombCooldown,
                _balance.BreakBombCollapseDuration, _balance.BreakBombCooldownMin);
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
            if (!_initialized) return;

            float dt = Time.deltaTime;

            // Domain ticks
            _tileTimerService.Tick(dt);
            _spreadService.Tick(dt);
            _fireDamageTickService.Tick(dt, _players, _stageModel);
            _p1Cooldown.Tick(dt);
            _p2Cooldown.Tick(dt);
            _player1.Invulnerability.Tick(dt);
            _player2.Invulnerability.Tick(dt);
            _player1.ForcedMove.Tick(dt);
            _player2.ForcedMove.Tick(dt);
            _tracker.Tick(dt, _players);

            // Presenter ticks
            _p1Presenter.Tick(dt);
            _p2Presenter.Tick(dt);
            _bombPresenter.Tick(dt);

            // Player input
            HandleP1Movement(dt);
            HandleP2Movement(dt);

            // Bomb input — P1
            if (Input.GetKeyDown(KeyCode.F)) StartBomb(PlayerId.Player1, BombType.Fire);
            if (Input.GetKeyUp(KeyCode.F)) _tracker.ReleaseBomb(PlayerId.Player1, _players);
            if (Input.GetKeyDown(KeyCode.G)) StartBomb(PlayerId.Player1, BombType.Break);
            if (Input.GetKeyUp(KeyCode.G)) _tracker.ReleaseBomb(PlayerId.Player1, _players);

            // Bomb input — P2
            if (Input.GetKeyDown(KeyCode.I)) StartBomb(PlayerId.Player2, BombType.Fire);
            if (Input.GetKeyUp(KeyCode.I)) _tracker.ReleaseBomb(PlayerId.Player2, _players);
            if (Input.GetKeyDown(KeyCode.O)) StartBomb(PlayerId.Player2, BombType.Break);
            if (Input.GetKeyUp(KeyCode.O)) _tracker.ReleaseBomb(PlayerId.Player2, _players);

            // Effect range
            if (Input.GetKeyDown(KeyCode.Alpha1)) SetEffectRange(1);
            if (Input.GetKeyDown(KeyCode.Alpha2)) SetEffectRange(2);
            if (Input.GetKeyDown(KeyCode.Alpha3)) SetEffectRange(3);
            if (Input.GetKeyDown(KeyCode.Alpha4)) SetEffectRange(4);
            if (Input.GetKeyDown(KeyCode.Alpha5)) SetEffectRange(5);

            // Max flight distance +/-
            if (Input.GetKeyDown(KeyCode.Equals) || Input.GetKeyDown(KeyCode.Plus)
                || Input.GetKeyDown(KeyCode.KeypadPlus))
                SetMaxFlight(_maxFlightOverride + 1);
            if (Input.GetKeyDown(KeyCode.Minus) || Input.GetKeyDown(KeyCode.KeypadMinus))
                SetMaxFlight(_maxFlightOverride - 1);

            // Damage / Reset
            if (Input.GetKeyDown(KeyCode.H)) DamagePlayer(_player1, 1);
            if (Input.GetKeyDown(KeyCode.J)) DamagePlayer(_player2, 1);
            if (Input.GetKeyDown(KeyCode.R)) ResetAll();
        }

        private void SetEffectRange(int range)
        {
            _effectRange = range;
            UnityEngine.Debug.Log("[BombPreview] 効果範囲: " + _effectRange);
        }

        private void SetMaxFlight(int value)
        {
            _maxFlightOverride = Mathf.Clamp(value, 1, 15);
            UnityEngine.Debug.Log("[BombPreview] 最大飛行距離: " + _maxFlightOverride);
        }

        // ─── Bomb Launch ──────────────────────────────────────────

        private void StartBomb(PlayerId owner, BombType type)
        {
            var player = owner == PlayerId.Player1 ? _player1 : _player2;
            var build = player.Build;
            int min = _balance.BombMinFlightDistance;
            BombSpec spec;
            if (type == BombType.Fire)
            {
                spec = new BombSpec(BombType.Fire,
                    _maxFlightOverride, min, _effectRange,
                    build.FireDamage, build.FireCooldown,
                    build.FireWallPenetration,
                    build.FireDuration, 0f, 0f);
            }
            else
            {
                spec = new BombSpec(BombType.Break,
                    _maxFlightOverride, min, _effectRange,
                    build.BreakDamage, build.BreakCooldown,
                    true,
                    0f, build.BreakCollapseTime,
                    _balance.BreakBombRecoveryDuration);
            }
            _tracker.StartFlight(owner, player.CurrentPosition, player.CurrentFacing, spec);
        }

        // ─── Player Movement ──────────────────────────────────────

        private void HandleP1Movement(float dt)
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

        private void HandleP2Movement(float dt)
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

        // ─── Actions ──────────────────────────────────────────────

        private void DamagePlayer(PlayerModel player, int amount)
        {
            var occupied = new HashSet<GridPos>
                { _player1.CurrentPosition, _player2.CurrentPosition };
            _damageService.ApplyDamage(
                player, amount, false, _stageModel, _safeTileSearch, occupied);
            UnityEngine.Debug.Log(
                $"[BombPreview] {player.Id} にダメージ {amount} → HP {player.Stats.CurrentHp.CurrentValue}");
        }

        private void ResetAll()
        {
            _initialized = false;

            // Cleanup bomb presentation
            _bombPresenter?.Dispose();
            _bombAnimService?.Dispose();
            _bombVfxPool?.Dispose();
            _tracker?.Dispose();
            _p1Cooldown?.Dispose();
            _p2Cooldown?.Dispose();

            // Cleanup player presentation
            _p1Presenter?.Dispose();
            _p2Presenter?.Dispose();
            _playerAnimService?.Dispose();
            _player1?.Dispose();
            _player2?.Dispose();
            if (_p1View != null) Destroy(_p1View.gameObject);
            if (_p2View != null) Destroy(_p2View.gameObject);

            // Recreate
            SetupPlayers();
            SetupBombs();
            SetupBombPresentation();
            _initialized = true;
            UnityEngine.Debug.Log("[BombPreview] リセット完了");
        }

        private void LogControls()
        {
            UnityEngine.Debug.Log("[BombPreview] 操作ガイド:");
            UnityEngine.Debug.Log("  W/A/S/D: P1 移動  |  矢印キー: P2 移動");
            UnityEngine.Debug.Log("  F (ホールド): P1 炎ボム  |  G (ホールド): P1 ブレークボム");
            UnityEngine.Debug.Log("  I (ホールド): P2 炎ボム  |  O (ホールド): P2 ブレークボム");
            UnityEngine.Debug.Log("  1-5: 効果範囲変更 (現在=" + _effectRange + ")");
            UnityEngine.Debug.Log("  +/-: 最大飛行距離変更 (現在=" + _maxFlightOverride + ")");
            UnityEngine.Debug.Log("  H: P1 ダメージ  |  J: P2 ダメージ  |  R: リセット");
        }

        private void OnDestroy()
        {
            _bombPresenter?.Dispose();
            _bombAnimService?.Dispose();
            _bombVfxPool?.Dispose();
            _tracker?.Dispose();
            _p1Cooldown?.Dispose();
            _p2Cooldown?.Dispose();
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
