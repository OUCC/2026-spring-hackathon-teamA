using System;
using System.Collections.Generic;
using UnityEngine;
using FloorBreaker.Shared.Application.Interfaces;
using FloorBreaker.Shared.Domain.Grid;
using FloorBreaker.Shared.Presentation.Common;
using FloorBreaker.ScriptableObjects.Configs;
using FloorBreaker.Stage.Domain;
using FloorBreaker.Stage.Presentation;

namespace FloorBreaker.UI.Title.Presentation
{
    /// <summary>
    /// オフスクリーンでステージを生成・レンダリングし RenderTexture を提供する。
    /// ステージプレビュー用カメラは常時レンダリング（VFX/アニメーション対応）。
    /// ギミックプレビューは GimmickSimulation を使ったライブシミュレーション。
    /// </summary>
    public sealed class StagePreviewRenderer : IDisposable
    {
        private const int PreviewLayer = 20;
        private static readonly Vector3 StageOffset = new(1000f, 1000f, 0f);
        private static readonly Vector3 GimmickBaseOffset = new(1100f, 1000f, 0f);

        private readonly GameObject _tilePrefab;
        private readonly TileSpriteConfig _spriteConfig;
        private readonly IBalanceParameters _balance;

        private GameObject _root;

        // ステージプレビュー
        private Camera _stageCamera;
        private RenderTexture _stageRT;
        private GameObject _stageObjects;
        private StagePresenter _stagePresenter;
        private TileFireVfxPool _stageFireVfxPool;

        // ギミックプレビュー
        private readonly List<GimmickSimEntry> _gimmickEntries = new();

        private struct GimmickSimEntry
        {
            public Camera Camera;
            public RenderTexture RT;
            public GimmickSimulation Simulation;
        }

        public StagePreviewRenderer(
            GameObject tilePrefab,
            TileSpriteConfig spriteConfig,
            IBalanceParameters balance)
        {
            _tilePrefab = tilePrefab;
            _spriteConfig = spriteConfig;
            _balance = balance;

            _root = new GameObject("[StagePreviewRenderer]");
            UnityEngine.Object.DontDestroyOnLoad(_root);
        }

        // ═══════════════════════════════════════════
        //  ステージ全体プレビュー
        // ═══════════════════════════════════════════

        public RenderTexture RenderStagePreview(StageConfig config, IRandomProvider random)
        {
            ClearStageObjects();

            // StageModel 生成
            var bounds = new TileCoordRange(0, 0, config.Width - 1, config.Height - 1);
            var model = new StageModel(bounds);

            // 壁生成
            var wallService = new WallGenerationService(
                config.WallSeedPercent, config.WallGrowthChance,
                config.WallTargetPercent, config.SpawnProtectionRadius);
            var wallPositions = wallService.Generate(bounds, new List<GridPos>(), random);
            foreach (var pos in wallPositions)
                model.SetTileData(pos, new TileData { Type = TileType.Wall, Condition = TileCondition.Intact });

            // ガス脈生成
            if (config.GasVeinCount > 0)
                GenerateGasVeins(model, config, bounds, random);

            // プリセットタイル
            if (config.PresetTiles != null)
            {
                foreach (var preset in config.PresetTiles)
                {
                    var pos = new GridPos(preset.x, preset.y);
                    if (!model.IsInBounds(pos)) continue;
                    model.SetTileData(pos, new TileData { Type = preset.type, Condition = preset.condition, WarpPairId = preset.warpPairId });
                }
            }

            // TileView 生成
            _stageObjects = new GameObject("PreviewStage");
            _stageObjects.transform.SetParent(_root.transform, false);
            var views = new Dictionary<GridPos, TileView>();

            foreach (var pos in bounds.GetAllPositions())
            {
                var worldPos = pos.ToWorldCenter().ToVector3(0f) + StageOffset;
                var go = UnityEngine.Object.Instantiate(_tilePrefab, worldPos, Quaternion.identity, _stageObjects.transform);
                SetLayerRecursive(go, PreviewLayer);

                var renderer = go.GetComponent<SpriteRenderer>();
                var tileView = go.GetComponent<TileView>();
                if (tileView == null) tileView = go.AddComponent<TileView>();

                tileView.Initialize(pos, renderer);
                tileView.ApplyState(model.GetTileData(pos), _spriteConfig);
                views[pos] = tileView;
            }

            // StagePresenter 接続（EternalFire/OnFire タイルの VFX スポーン用）
            _stagePresenter?.Dispose();
            if (_spriteConfig.FireVfxPrefab != null)
            {
                var poolParent = new GameObject("StageFireVfxPool");
                poolParent.transform.SetParent(_stageObjects.transform, false);
                _stageFireVfxPool = new TileFireVfxPool(_spriteConfig.FireVfxPrefab, poolParent.transform, 30, PreviewLayer);
            }
            var animService = new TileAnimationService(_spriteConfig);
            _stagePresenter = new StagePresenter(model, views, animService, _stageFireVfxPool, _spriteConfig, null);

            // カメラ設定（常時レンダリング）
            if (_stageCamera == null)
                _stageCamera = CreateCamera("StagePreviewCam");

            float centerX = config.Width * 0.5f + StageOffset.x;
            float centerY = config.Height * 0.5f + StageOffset.y;
            _stageCamera.transform.position = new Vector3(centerX, centerY, -10f);
            _stageCamera.orthographicSize = Mathf.Max(config.Width, config.Height) * 0.55f;

            if (_stageRT != null) _stageRT.Release();
            _stageRT = new RenderTexture(512, 512, 0);
            _stageCamera.targetTexture = _stageRT;
            _stageCamera.enabled = true;

            return _stageRT;
        }

        // ═══════════════════════════════════════════
        //  ギミックプレビュー（ライブシミュレーション）
        // ═══════════════════════════════════════════

        public RenderTexture RenderGimmickPreview(GimmickType type)
        {
            int index = _gimmickEntries.Count;
            var offset = GimmickBaseOffset + new Vector3(0f, index * 30f, 0f);

            // カメラ生成
            var cam = CreateCamera($"GimmickCam_{type}");
            float center = 7 * 0.5f; // GridSize / 2
            cam.transform.position = new Vector3(center + offset.x, center + offset.y, -10f);
            cam.orthographicSize = 7 * 0.55f;

            var rt = new RenderTexture(256, 256, 0);
            cam.targetTexture = rt;
            cam.enabled = true;

            // シミュレーション生成
            var sim = new GimmickSimulation(type, _spriteConfig, _tilePrefab, offset, _root.transform);

            _gimmickEntries.Add(new GimmickSimEntry { Camera = cam, RT = rt, Simulation = sim });

            return rt;
        }

        /// <summary>毎フレーム呼ぶ。全ギミックシミュレーションを更新する。</summary>
        public void Tick(float deltaTime)
        {
            foreach (var entry in _gimmickEntries)
                entry.Simulation?.Tick(deltaTime);
        }

        public void Dispose()
        {
            ClearStageObjects();
            ClearGimmickSimulations();

            if (_stageCamera != null) UnityEngine.Object.Destroy(_stageCamera.gameObject);
            if (_stageRT != null) { _stageRT.Release(); _stageRT = null; }

            if (_root != null) UnityEngine.Object.Destroy(_root);
        }

        // ═══════════════════════════════════════════
        //  Internal
        // ═══════════════════════════════════════════

        private Camera CreateCamera(string name)
        {
            var camObj = new GameObject(name);
            camObj.transform.SetParent(_root.transform, false);
            camObj.layer = PreviewLayer;

            var cam = camObj.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 16f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 100f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.08f, 0.06f, 0.1f, 1f);
            cam.cullingMask = 1 << PreviewLayer;
            cam.enabled = false;
            return cam;
        }

        private void ClearStageObjects()
        {
            _stagePresenter?.Dispose();
            _stagePresenter = null;
            _stageFireVfxPool = null;

            if (_stageObjects != null)
            {
                UnityEngine.Object.Destroy(_stageObjects);
                _stageObjects = null;
            }
        }

        public void ClearGimmickSimulations()
        {
            foreach (var entry in _gimmickEntries)
            {
                entry.Simulation?.Dispose();
                if (entry.Camera != null) UnityEngine.Object.Destroy(entry.Camera.gameObject);
                entry.RT?.Release();
            }
            _gimmickEntries.Clear();
        }

        private static void GenerateGasVeins(StageModel model, StageConfig config, TileCoordRange bounds, IRandomProvider random)
        {
            var dirs = new (int dx, int dy)[] { (1, 0), (-1, 0), (0, 1), (0, -1) };

            for (int v = 0; v < config.GasVeinCount; v++)
            {
                int sx = random.Range(bounds.MinX + 3, bounds.MaxX - 2);
                int sy = random.Range(bounds.MinY + 3, bounds.MaxY - 2);

                int length = random.Range(config.GasVeinMinLength, config.GasVeinMaxLength + 1);
                int cx = sx, cy = sy;
                var dir = dirs[random.Range(0, 4)];

                for (int step = 0; step < length; step++)
                {
                    var pos = new GridPos(cx, cy);
                    if (!model.IsInBounds(pos)) break;

                    var existing = model.GetTileData(pos);
                    if (existing.Type == TileType.Wall || existing.Type == TileType.Bedrock)
                        break;
                    if (existing.Type != TileType.Gas && existing.Condition == TileCondition.Intact)
                        model.SetTileData(pos, new TileData { Type = TileType.Gas, Condition = TileCondition.Intact, WarpPairId = -1 });

                    if (random.Range(0, 100) < 30)
                    {
                        if (dir.dx != 0)
                            dir = random.Range(0, 2) == 0 ? (0, 1) : (0, -1);
                        else
                            dir = random.Range(0, 2) == 0 ? (1, 0) : (-1, 0);
                    }

                    cx += dir.dx;
                    cy += dir.dy;
                }
            }
        }

        private static void SetLayerRecursive(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform child in go.transform)
                SetLayerRecursive(child.gameObject, layer);
        }
    }

    public enum GimmickType
    {
        Gas,
        Bedrock,
        Warp,
        EternalFire,
    }
}
