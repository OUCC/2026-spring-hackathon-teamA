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
        private readonly StageGenerationService _stageGen;

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
            StageGenerationService stageGen)
        {
            _tilePrefab = tilePrefab;
            _spriteConfig = spriteConfig;
            _stageGen = stageGen;

            _root = new GameObject("[StagePreviewRenderer]");
            UnityEngine.Object.DontDestroyOnLoad(_root);
        }

        // ═══════════════════════════════════════════
        //  ステージ全体プレビュー
        // ═══════════════════════════════════════════

        public RenderTexture RenderStagePreview(StageConfig config, IRandomProvider random)
        {
            ClearStageObjects();

            // StageModel 生成 + タイル配置（Domain サービスに委譲）
            var bounds = new TileCoordRange(0, 0, config.Width - 1, config.Height - 1);
            var model = new StageModel(bounds);

            var genParams = new StageGenerationParams
            {
                WallSeedPercent = config.WallSeedPercent,
                WallGrowthChance = config.WallGrowthChance,
                WallTargetPercent = config.WallTargetPercent,
                SpawnProtectionRadius = config.SpawnProtectionRadius,
                GasVeinCount = config.GasVeinCount,
                GasVeinMinLength = config.GasVeinMinLength,
                GasVeinMaxLength = config.GasVeinMaxLength,
                PresetTiles = config.PresetTiles,
            };
            _stageGen.PopulateStage(model, genParams, new List<GridPos>(), random);

            // TileView 生成
            _stageObjects = new GameObject("PreviewStage");
            _stageObjects.transform.SetParent(_root.transform, false);
            var views = new Dictionary<GridPos, TileView>();

            foreach (var pos in bounds.GetAllPositions())
            {
                var worldPos = pos.ToWorldCenter().ToVector3(0f) + StageOffset;
                var go = UnityEngine.Object.Instantiate(_tilePrefab, worldPos, Quaternion.identity, _stageObjects.transform);
                go.SetLayerRecursive(PreviewLayer);

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

    }
}
