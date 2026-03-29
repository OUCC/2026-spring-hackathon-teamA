using System;
using System.Collections.Generic;
using UnityEngine;
using FloorBreaker.Shared.Domain.Grid;
using FloorBreaker.Shared.Domain.Primitives;
using FloorBreaker.Shared.Presentation.Common;
using FloorBreaker.Stage.Domain;
using FloorBreaker.Stage.Presentation;
using FloorBreaker.Bombs.Application;
using FloorBreaker.Bombs.Domain;
using FloorBreaker.Player.Domain;
using FloorBreaker.Player.Application;

namespace FloorBreaker.UI.Title.Presentation
{
    /// <summary>
    /// ギミックプレビュー用のライブシミュレーション。
    /// ミニステージを生成し、ゲームサービス群を Tick してギミックの動作を実演する。
    /// StagePresenter が TileChanged を購読して VFX/アニメーションを自動発火する。
    /// </summary>
    public sealed class GimmickSimulation : IDisposable
    {
        private const int PreviewLayer = 20;
        private const int GridSize = 7;
        private const int Mid = GridSize / 2;

        private readonly GimmickType _type;
        private readonly TileSpriteConfig _spriteConfig;
        private readonly GameObject _tilePrefab;
        private readonly Vector3 _worldOffset;

        // Domain
        private StageModel _stage;
        private TileTimerService _tileTimer;
        private BombEffectSpreadService _spreadService;
        private GasIgnitionService _gasIgnition;

        // Presentation
        private Dictionary<GridPos, TileView> _views;
        private StagePresenter _presenter;
        private TileFireVfxPool _fireVfxPool;
        private TileAnimationService _animService;
        private GameObject _stageRoot;

        // Simulation state
        private float _elapsed;
        private bool _triggered;
        private readonly float _triggerTime;
        private readonly float _loopDuration;

        // Initial tile data for reset
        private TileData[,] _initialTiles;

        public GimmickSimulation(
            GimmickType type,
            TileSpriteConfig spriteConfig,
            GameObject tilePrefab,
            Vector3 worldOffset,
            Transform parent)
        {
            _type = type;
            _spriteConfig = spriteConfig;
            _tilePrefab = tilePrefab;
            _worldOffset = worldOffset;

            _triggerTime = type == GimmickType.EternalFire ? 0f : 1.0f;
            _loopDuration = type == GimmickType.EternalFire ? float.MaxValue : 6.0f;

            BuildStage(parent);
            BuildServices();
            BuildPresentation(parent);
            SaveInitialState();
        }

        public void Tick(float deltaTime)
        {
            _elapsed += deltaTime;

            if (!_triggered && _elapsed >= _triggerTime)
            {
                _triggered = true;
                TriggerGimmick();
            }

            // Service tick (same order as MatchPhaseScheduler)
            _tileTimer?.Tick(deltaTime);
            _spreadService?.Tick(deltaTime);
            _gasIgnition?.Tick(deltaTime);

            // Presentation tick
            _presenter?.TickFireDecay();
            _presenter?.TickRecoveryPreview();

            // Loop reset
            if (_elapsed >= _loopDuration)
                Reset();
        }

        public void Dispose()
        {
            _presenter?.Dispose();
            _fireVfxPool?.DespawnAll();
            _stage?.Dispose();

            if (_stageRoot != null)
                UnityEngine.Object.Destroy(_stageRoot);
        }

        // ═══════════════════════════════════════════
        //  Setup
        // ═══════════════════════════════════════════

        private void BuildStage(Transform parent)
        {
            var bounds = TileCoordRange.FromSize(GridSize);
            _stage = new StageModel(bounds);

            // ギミック固有のタイル配置
            switch (_type)
            {
                case GimmickType.Gas:
                    // 横一列ガス + 縦分岐
                    for (int x = 0; x < GridSize; x++)
                        _stage.SetTileData(new GridPos(x, Mid), new TileData { Type = TileType.Gas, Condition = TileCondition.Intact });
                    _stage.SetTileData(new GridPos(Mid, Mid - 1), new TileData { Type = TileType.Gas, Condition = TileCondition.Intact });
                    _stage.SetTileData(new GridPos(Mid, Mid - 2), new TileData { Type = TileType.Gas, Condition = TileCondition.Intact });
                    break;

                case GimmickType.Bedrock:
                    // 縦一列岩盤
                    for (int y = 0; y < GridSize; y++)
                        _stage.SetTileData(new GridPos(Mid, y), new TileData { Type = TileType.Bedrock, Condition = TileCondition.Intact });
                    break;

                case GimmickType.Warp:
                    // ワープペア
                    _stage.SetTileData(new GridPos(Mid - 2, Mid), new TileData { Type = TileType.Warp, Condition = TileCondition.Intact, WarpPairId = 1 });
                    _stage.SetTileData(new GridPos(Mid + 2, Mid), new TileData { Type = TileType.Warp, Condition = TileCondition.Intact, WarpPairId = 1 });
                    break;

                case GimmickType.EternalFire:
                    // 中央 3x3 永久炎
                    for (int x = Mid - 1; x <= Mid + 1; x++)
                        for (int y = Mid - 1; y <= Mid + 1; y++)
                            _stage.SetTileData(new GridPos(x, y), new TileData { Type = TileType.Normal, Condition = TileCondition.EternalFire });
                    break;
            }

            // TileView 生成
            _stageRoot = new GameObject($"GimmickSim_{_type}");
            _stageRoot.transform.SetParent(parent, false);

            var viewBounds = TileCoordRange.FromSize(GridSize);
            _views = new Dictionary<GridPos, TileView>();

            foreach (var pos in viewBounds.GetAllPositions())
            {
                var worldPos = pos.ToWorldCenter().ToVector3(0f) + _worldOffset;
                var go = UnityEngine.Object.Instantiate(_tilePrefab, worldPos, Quaternion.identity, _stageRoot.transform);
                go.SetLayerRecursive(PreviewLayer);

                var renderer = go.GetComponent<SpriteRenderer>();
                var tileView = go.GetComponent<TileView>();
                if (tileView == null) tileView = go.AddComponent<TileView>();

                tileView.Initialize(pos, renderer);
                tileView.ApplyState(_stage.GetTileData(pos), _spriteConfig);

                _views[pos] = tileView;
            }
        }

        private void BuildServices()
        {
            _tileTimer = new TileTimerService(_stage);

            if (_type == GimmickType.Gas || _type == GimmickType.Bedrock)
            {
                var safeTileSearch = new SafeTileSearchService();
                var damageService = new PlayerDamageService(1.5f, 1f, _stage, safeTileSearch);

                if (_type == GimmickType.Gas)
                {
                    _gasIgnition = new GasIgnitionService(_stage, _tileTimer, 0.15f, 3.0f);
                    _spreadService = new BombEffectSpreadService(
                        _stage, _tileTimer, damageService, safeTileSearch,
                        tileIgnitionHandler: _gasIgnition);
                }
                else
                {
                    _spreadService = new BombEffectSpreadService(
                        _stage, _tileTimer, damageService, safeTileSearch);
                }
            }
        }

        private void BuildPresentation(Transform parent)
        {
            _animService = new TileAnimationService(_spriteConfig);

            // FireVfxPool - レイヤー設定付き
            if (_spriteConfig.FireVfxPrefab != null)
            {
                var poolParent = new GameObject("FireVfxPool");
                poolParent.transform.SetParent(_stageRoot.transform, false);
                _fireVfxPool = new TileFireVfxPool(_spriteConfig.FireVfxPrefab, poolParent.transform, 10, PreviewLayer);
            }

            _presenter = new StagePresenter(_stage, _views, _animService, _fireVfxPool, _spriteConfig, null);
            _presenter.SetTileTimerService(_tileTimer);
        }

        private void SaveInitialState()
        {
            _initialTiles = new TileData[GridSize, GridSize];
            for (int x = 0; x < GridSize; x++)
                for (int y = 0; y < GridSize; y++)
                    _initialTiles[x, y] = _stage.GetTileData(new GridPos(x, y));
        }

        // ═══════════════════════════════════════════
        //  Trigger & Reset
        // ═══════════════════════════════════════════

        private void TriggerGimmick()
        {
            switch (_type)
            {
                case GimmickType.Gas:
                    TriggerFireBombOnGas();
                    break;
                case GimmickType.Bedrock:
                    TriggerBreakBombNearBedrock();
                    break;
                case GimmickType.Warp:
                    // ワープはタイル状態変更不要 - VFX のみ
                    break;
                case GimmickType.EternalFire:
                    // 永久炎は初期状態で StagePresenter が自動的に VFX スポーン済み
                    break;
            }
        }

        private void TriggerFireBombOnGas()
        {
            var center = new GridPos(0, Mid);
            var spec = new BombSpec(BombType.Fire, 3, 3, 1, 1, 3.0f, false, 0f, 2f, 0f, false);
            var result = BombResolverHelper.ResolveFireBomb(center, spec, _stage);
            _spreadService.EnqueueFireBomb(result, center, Array.Empty<PlayerModel>(), null, 0.15f);
        }

        private void TriggerBreakBombNearBedrock()
        {
            var center = new GridPos(Mid - 1, Mid);
            var spec = new BombSpec(BombType.Break, 3, 3, 1, 2, 0f, false, 0f, 3f, 5f, false);
            var result = BombResolverHelper.ResolveBreakBomb(center, spec, _stage);
            _spreadService.EnqueueBreakBomb(result, center, Array.Empty<PlayerModel>(), null, 0.3f);
        }

        private void Reset()
        {
            _elapsed = 0f;
            _triggered = false;

            // VFX を全てクリア
            _fireVfxPool?.DespawnAll();

            // タイル状態をリセット
            for (int x = 0; x < GridSize; x++)
                for (int y = 0; y < GridSize; y++)
                {
                    var pos = new GridPos(x, y);
                    _stage.SetTileData(pos, _initialTiles[x, y]);
                    if (_views.TryGetValue(pos, out var view))
                        view.ApplyState(_initialTiles[x, y], _spriteConfig);
                }

            // サービス再構築（内部キュー等をクリーンにする）
            _presenter?.Dispose();
            BuildServices();
            _presenter = new StagePresenter(_stage, _views, _animService, _fireVfxPool, _spriteConfig, null);
            _presenter.SetTileTimerService(_tileTimer);
        }

    }
}
