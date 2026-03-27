using System;
using System.Collections.Generic;
using DG.Tweening;
using R3;
using UnityEngine;
using UnityEngine.InputSystem;
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
using FloorBreaker.Bombs.Presentation;
using FloorBreaker.Slimes.Domain;
using FloorBreaker.ScriptableObjects.Balance;
using FloorBreaker.Shared.Infrastructure.Audio;

namespace FloorBreaker.UI.Title.Presentation
{
    /// <summary>
    /// タイトル画面の左右にプレイヤーが試せるテスト空間を配置する。
    /// BombPreviewController パターン踏襲、DI 不使用。
    /// 2つの独立したミニグリッドを異なるワールドオフセットに配置する。
    /// </summary>
    public sealed class TitleTestSpaceController : MonoBehaviour
    {
        [Header("Factories (シーンに配置)")]
        [SerializeField] private StageViewFactory _stageFactory;
        [SerializeField] private PlayerViewFactory _playerFactory;
        [SerializeField] private BombViewFactory _bombFactory;

        [Header("Settings")]
        [SerializeField] private BalanceConfig _balance;
        [SerializeField] private InputActionAsset _inputActions;

        [Header("Layout")]
        [SerializeField] private Vector3 _p1Offset = new(-12f, 1f, 0f);
        [SerializeField] private Vector3 _p2Offset = new(20f, 1f, 0f);
        [SerializeField] private int _gridSize = 8;
        [SerializeField] private float _dimAlpha = 0.35f;

        private TestArea _p1Area;
        private TestArea _p2Area;
        private bool _initialized;

        private void Start()
        {
            if (_stageFactory == null || _playerFactory == null || _bombFactory == null
                || _balance == null || _inputActions == null)
            {
                Debug.LogError("[TitleTestSpace] SerializeField が未設定です。");
                return;
            }

            _p1Area = new TestArea(this, PlayerId.Player1, _p1Offset, _gridSize, _dimAlpha,
                _stageFactory, _playerFactory, _bombFactory, _balance, _inputActions);
            _p2Area = new TestArea(this, PlayerId.Player2, _p2Offset, _gridSize, _dimAlpha,
                _stageFactory, _playerFactory, _bombFactory, _balance, _inputActions);

            _p1Area.Initialize();
            _p2Area.Initialize();

            // カメラ調整
            var cam = Camera.main;
            if (cam != null)
            {
                cam.orthographic = true;
                cam.orthographicSize = 12f;
                // 両エリアの中間点
                float centerX = (_p1Offset.x + _p2Offset.x + _gridSize) * 0.5f;
                float centerY = (_p1Offset.y + _p2Offset.y + _gridSize) * 0.5f;
                cam.transform.position = new Vector3(centerX, centerY, -10f);
            }

            _initialized = true;
        }

        private void Update()
        {
            if (!_initialized) return;
            float dt = Time.deltaTime;
            _p1Area.Tick(dt);
            _p2Area.Tick(dt);
        }

        private void OnDestroy()
        {
            _p1Area?.Dispose();
            _p2Area?.Dispose();
        }

        // ═══════════════════════════════════════════════════════════
        //  TestArea — 1プレイヤー分の独立したミニゲーム環境
        // ═══════════════════════════════════════════════════════════
        private sealed class TestArea : IDisposable
        {
            private readonly TitleTestSpaceController _owner;
            private readonly PlayerId _playerId;
            private readonly Vector3 _offset;
            private readonly int _gridSize;
            private readonly float _dimAlpha;
            private readonly BalanceConfig _balance;
            private readonly InputActionAsset _inputActions;

            // Factories (shared references)
            private readonly StageViewFactory _stageFactory;
            private readonly PlayerViewFactory _playerFactory;
            private readonly BombViewFactory _bombFactory;

            // Domain
            private StageModel _stageModel;
            private TileTimerService _tileTimerService;
            private StageQueryService _queryService;
            private PlayerModel _player;
            private PlayerMoveService _moveService;
            private BombCooldownState _cooldown;
            private BombFlightTracker _tracker;
            private BombLaunchUseCase _launchUseCase;
            private BombEffectSpreadService _spreadService;
            private SlimeRegistry _slimeRegistry;
            private SafeTileSearchService _safeTileSearch;
            private List<PlayerModel> _players;

            // Presentation
            private Transform _areaRoot;
            private Dictionary<GridPos, TileView> _tileViews;
            private TileAnimationService _tileAnimService;
            private TileFireVfxPool _fireVfxPool;
            private StagePresenter _stagePresenter;
            private PlayerView _playerView;
            private PlayerAnimationService _playerAnimService;
            private BombAnimationService _bombAnimService;
            private BombExplosionVfxPool _bombVfxPool;

            // Input
            private InputActionMap _gameplayMap;
            private InputAction _moveAction;
            private InputAction _fireBombAction;
            private InputAction _breakBombAction;
            private Direction8 _lastDirection = Direction8.S;
            private Direction8? _heldDirection;
            private float _moveTimer;
            private const float MoveRepeatRate = 0.12f;
            private const float MoveFirstDelay = 0.01f;
            private bool _firstMoveDone;

            // Bomb flight view tracking
            private BombFlightView _activeBombView;
            private Tween _activeBombTween;

            // R3
            private readonly CompositeDisposable _subscriptions = new();

            public TestArea(
                TitleTestSpaceController owner, PlayerId playerId, Vector3 offset,
                int gridSize, float dimAlpha,
                StageViewFactory stageFactory, PlayerViewFactory playerFactory,
                BombViewFactory bombFactory, BalanceConfig balance, InputActionAsset inputActions)
            {
                _owner = owner;
                _playerId = playerId;
                _offset = offset;
                _gridSize = gridSize;
                _dimAlpha = dimAlpha;
                _stageFactory = stageFactory;
                _playerFactory = playerFactory;
                _bombFactory = bombFactory;
                _balance = balance;
                _inputActions = inputActions;
            }

            public void Initialize()
            {
                // Area root
                _areaRoot = new GameObject($"TestArea_{_playerId}").transform;
                _areaRoot.SetParent(_owner.transform, false);
                _areaRoot.position = _offset;

                SetupDomain();
                SetupInput();
                SetupPresentation();
                SubscribeEvents();
            }

            private void SetupDomain()
            {
                var bounds = TileCoordRange.FromSize(_gridSize);
                _stageModel = new StageModel(bounds);
                _tileTimerService = new TileTimerService(_stageModel);
                _safeTileSearch = new SafeTileSearchService();
                _queryService = new StageQueryService(_stageModel);
                _slimeRegistry = new SlimeRegistry();
                _moveService = new PlayerMoveService();

                // 軽量な壁生成（10%密度）
                IRandomProvider random = new SeededRandomProvider(
                    _playerId == PlayerId.Player1 ? 42 : 99);
                var wallGen = new WallGenerationService(0.05f, 0.3f, 0.10f, 2);
                var spawn = new GridPos(_gridSize / 2, _gridSize / 2);
                var walls = wallGen.Generate(bounds, spawn, spawn, random);
                foreach (var pos in walls)
                    _stageModel.SetTileState(pos, TileState.Wall);

                // プレイヤー
                var stats = new PlayerStats(_balance.InitialHp, _balance.BaseMovementSpeed, _balance.MaxMovementSpeed);
                var build = new PlayerBuild(
                    _balance.FireBombMaxFlightDistance, _balance.FireBombEffectRange,
                    _balance.FireBombContactDamage, _balance.FireBombCooldown,
                    _balance.FireBombDuration, _balance.FireBombDefaultWallPenetration,
                    _balance.FireBombCooldownMin,
                    _balance.BreakBombMaxFlightDistance, _balance.BreakBombEffectRange,
                    _balance.BreakBombDamage, _balance.BreakBombCooldown,
                    _balance.BreakBombCollapseDuration, _balance.BreakBombCooldownMin);
                _player = new PlayerModel(_playerId, spawn, stats, build);
                _players = new List<PlayerModel> { _player };

                // ボム
                _cooldown = new BombCooldownState();
                var dummyCooldown = new BombCooldownState();
                var areaResolver = new BombAreaResolver(_queryService);
                var landingResolver = new BombLandingResolver(_stageModel);
                var breakResolver = new BreakBombResolver(areaResolver);
                var fireResolver = new FireBombResolver(areaResolver);

                _spreadService = new BombEffectSpreadService(
                    _stageModel, _tileTimerService, null, _safeTileSearch, _slimeRegistry);

                _launchUseCase = new BombLaunchUseCase(
                    landingResolver, breakResolver, fireResolver,
                    _stageModel, _balance, _spreadService);

                // P1/P2 で正しいクールダウンスロットを割り当て
                var p1cd = _playerId == PlayerId.Player1 ? _cooldown : dummyCooldown;
                var p2cd = _playerId == PlayerId.Player2 ? _cooldown : dummyCooldown;
                _tracker = new BombFlightTracker(
                    _launchUseCase, p1cd, p2cd, _stageModel, _slimeRegistry, _balance);
            }

            private void SetupInput()
            {
                string mapName = _playerId == PlayerId.Player1 ? "Gameplay_P1" : "Gameplay_P2";
                _gameplayMap = _inputActions.FindActionMap(mapName);
                if (_gameplayMap == null) return;

                _gameplayMap.Enable();
                _moveAction = _gameplayMap.FindAction("Move");
                _fireBombAction = _gameplayMap.FindAction("FireBombHold");
                _breakBombAction = _gameplayMap.FindAction("BreakBombHold");

                if (_fireBombAction != null)
                {
                    _fireBombAction.started += OnBombStarted;
                    _fireBombAction.canceled += OnBombCanceled;
                }
                if (_breakBombAction != null)
                {
                    _breakBombAction.started += OnBombStarted;
                    _breakBombAction.canceled += OnBombCanceled;
                }
            }

            private void SetupPresentation()
            {
                var bounds = _stageModel.GetCurrentBounds();
                var tileConfig = _stageFactory.Config;

                // タイルビュー生成 → オフセット適用
                _tileViews = _stageFactory.CreateTileViews(_stageModel, bounds);
                // StageRoot (factory の最後の子) をオフセット位置に移動
                if (_stageFactory.transform.childCount > 0)
                {
                    var stageRoot = _stageFactory.transform.GetChild(
                        _stageFactory.transform.childCount - 1);
                    stageRoot.SetParent(_areaRoot, false);
                    stageRoot.localPosition = Vector3.zero;
                }

                // タイル半透明化
                foreach (var kv in _tileViews)
                {
                    var sr = kv.Value.GetComponent<SpriteRenderer>();
                    if (sr != null)
                    {
                        var c = sr.color;
                        c.a = _dimAlpha;
                        sr.color = c;
                    }
                }

                _tileAnimService = new TileAnimationService(tileConfig);

                // Fire VFX pool
                var vfxParent = new GameObject("TileVfxPool").transform;
                vfxParent.SetParent(_areaRoot, false);
                var fireVfxPrefab = tileConfig.FireVfxPrefab;
                _fireVfxPool = fireVfxPrefab != null
                    ? new TileFireVfxPool(fireVfxPrefab, vfxParent)
                    : null;

                var nullAudio = new NullAudioService();
                _stagePresenter = new StagePresenter(
                    _stageModel, _tileViews, _tileAnimService, _fireVfxPool, tileConfig, nullAudio);

                // プレイヤービュー生成
                _playerView = _playerFactory.CreatePlayerView(_playerId, _player.CurrentPosition);
                _playerView.transform.SetParent(_areaRoot, false);
                _playerView.transform.localPosition =
                    _player.CurrentPosition.ToWorldCenter().ToVector3(-1f);
                DimSprite(_playerView.GetComponent<SpriteRenderer>());

                _playerAnimService = new PlayerAnimationService(_playerFactory.Config);

                // ボム VFX
                var bombConfig = _bombFactory.Config;
                _bombAnimService = new BombAnimationService(bombConfig);
                var bombVfxParent = new GameObject("BombVfxPool").transform;
                bombVfxParent.SetParent(_areaRoot, false);
                _bombVfxPool = new BombExplosionVfxPool(
                    bombConfig.GetExplosionPrefab(BombType.Fire),
                    bombConfig.GetExplosionPrefab(BombType.Break),
                    bombVfxParent, bombConfig.ExplosionVfxScale, bombConfig.ExplosionVfxDuration);
            }

            private void SubscribeEvents()
            {
                // プレイヤー位置変更 → ビュー移動（オフセット込み）
                _player.Position.Subscribe(pos =>
                {
                    var localPos = pos.ToWorldCenter().ToVector3(-1f);
                    _playerAnimService.KillMoveTween(_playerId);
                    _playerView.transform
                        .DOLocalMove(localPos, 0.08f)
                        .SetEase(Ease.OutQuad)
                        .SetLink(_playerView.gameObject);
                }).AddTo(_subscriptions);

                // 向き変更 → スプライト
                var playerConfig = _playerFactory.Config;
                _player.FacingDirection.Subscribe(dir =>
                {
                    _playerView.SetDirection(dir, playerConfig);
                }).AddTo(_subscriptions);

                // ボム飛行開始
                _tracker.FlightStarted.Subscribe(OnFlightStarted).AddTo(_subscriptions);

                // ボム着弾
                _tracker.BombLanded.Subscribe(OnBombLanded).AddTo(_subscriptions);
            }

            public void Tick(float dt)
            {
                // Domain ticks
                _tileTimerService.Tick(dt);
                _spreadService.Tick(dt);
                _cooldown.Tick(dt);
                _tracker.Tick(dt, _players);
                _bombVfxPool?.Tick(dt);

                // Input → movement
                HandleMovement(dt);
            }

            // ─── Input ──────────────────────────────────────────

            private void HandleMovement(float dt)
            {
                if (_moveAction == null) return;

                var vec = _moveAction.ReadValue<Vector2>();
                var dir = Vector2ToDirection8(vec);

                if (dir.HasValue)
                {
                    _heldDirection = dir.Value;
                    _lastDirection = dir.Value;

                    _moveTimer -= dt;
                    if (!_firstMoveDone)
                    {
                        if (_moveTimer <= 0f)
                        {
                            _moveService.TryMove(_player, dir.Value, _stageModel);
                            _moveTimer = MoveRepeatRate;
                            _firstMoveDone = true;
                        }
                    }
                    else if (_moveTimer <= 0f)
                    {
                        _moveService.TryMove(_player, dir.Value, _stageModel);
                        _moveTimer = MoveRepeatRate;
                    }
                }
                else
                {
                    _heldDirection = null;
                    _moveTimer = MoveFirstDelay;
                    _firstMoveDone = false;
                }
            }

            private void OnBombStarted(InputAction.CallbackContext ctx)
            {
                var type = ctx.action == _fireBombAction ? BombType.Fire : BombType.Break;
                var spec = type == BombType.Fire
                    ? _launchUseCase.CreateFireBombSpec(_player.Build)
                    : _launchUseCase.CreateBreakBombSpec(_player.Build);
                _tracker.StartFlight(_playerId, _player.CurrentPosition, _lastDirection, spec);
            }

            private void OnBombCanceled(InputAction.CallbackContext ctx)
            {
                _tracker.ReleaseBomb(_playerId, _players);
            }

            // ─── Bomb Presentation ──────────────────────────────

            private void OnFlightStarted(BombFlightStartedEvent e)
            {
                if (e.Owner != _playerId) return;

                var startLocal = e.Origin.ToWorldCenter().ToVector3(-2f);
                var maxEndGridPos = e.Origin + e.Direction.ToOffset() * e.Spec.MaxFlightDistance;
                var endLocal = maxEndGridPos.ToWorldCenter().ToVector3(-2f);
                var startWorld = _areaRoot.TransformPoint(startLocal);
                var endWorld = _areaRoot.TransformPoint(endLocal);

                _activeBombView = _bombFactory.GetView(e.Owner, e.Spec.Type, startWorld);
                float duration = _balance.BombFlightSpeed > 0f
                    ? e.Spec.MaxFlightDistance / _balance.BombFlightSpeed
                    : 0.25f;

                _activeBombTween?.Kill();
                _activeBombTween = _activeBombView.transform
                    .DOMove(endWorld, duration)
                    .SetEase(Ease.Linear)
                    .SetLink(_activeBombView.gameObject);
            }

            private void OnBombLanded(BombLandedEvent e)
            {
                if (e.Owner != _playerId) return;

                _activeBombTween?.Kill();
                if (_activeBombView != null)
                {
                    var landWorld = _areaRoot.TransformPoint(
                        e.LandingPos.ToWorldCenter().ToVector3(-2f));
                    _activeBombView.SetPositionImmediate(landWorld);
                    _bombFactory.ReturnView(_activeBombView);
                    _activeBombView = null;
                }

                // VFX
                var vfxWorld = _areaRoot.TransformPoint(
                    e.LandingPos.ToWorldCenter().ToVector3(0f));
                _bombVfxPool?.Spawn(e.Type, vfxWorld);
            }

            // ─── Helpers ────────────────────────────────────────

            private void DimSprite(SpriteRenderer sr)
            {
                if (sr == null) return;
                var c = sr.color;
                c.a = _dimAlpha;
                sr.color = c;
            }

            private static Direction8? Vector2ToDirection8(Vector2 v)
            {
                if (v.sqrMagnitude < 0.1f) return null;
                float angle = Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg;
                if (angle < 0) angle += 360f;
                int sector = Mathf.RoundToInt(angle / 45f) % 8;
                return sector switch
                {
                    0 => Direction8.E,
                    1 => Direction8.NE,
                    2 => Direction8.N,
                    3 => Direction8.NW,
                    4 => Direction8.W,
                    5 => Direction8.SW,
                    6 => Direction8.S,
                    7 => Direction8.SE,
                    _ => null,
                };
            }

            public void Dispose()
            {
                _subscriptions.Dispose();

                if (_fireBombAction != null)
                {
                    _fireBombAction.started -= OnBombStarted;
                    _fireBombAction.canceled -= OnBombCanceled;
                }
                if (_breakBombAction != null)
                {
                    _breakBombAction.started -= OnBombStarted;
                    _breakBombAction.canceled -= OnBombCanceled;
                }

                _activeBombTween?.Kill();
                _bombPresenterDispose();
                _stagePresenter?.Dispose();
                _tileAnimService?.Dispose();
                _fireVfxPool?.Dispose();
                _bombAnimService?.Dispose();
                _bombVfxPool?.Dispose();
                _tracker?.Dispose();
                _cooldown?.Dispose();
                _playerAnimService?.Dispose();
                _tileTimerService?.Dispose();
                _player?.Dispose();
                _stageModel?.Dispose();
                _slimeRegistry?.Dispose();

                if (_areaRoot != null)
                    UnityEngine.Object.Destroy(_areaRoot.gameObject);
            }

            private void _bombPresenterDispose()
            {
                // Bomb view cleanup
                if (_activeBombView != null)
                {
                    _bombFactory.ReturnView(_activeBombView);
                    _activeBombView = null;
                }
            }
        }
    }
}
