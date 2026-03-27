using System.Collections.Generic;
using UnityEngine;
using FloorBreaker.Shared.Domain.Grid;
using FloorBreaker.Shared.Application.Interfaces;
using FloorBreaker.Shared.Infrastructure.Random;
using FloorBreaker.Shared.Presentation.Common;
using FloorBreaker.Shared.Domain.Primitives;
using FloorBreaker.Stage.Domain;
using FloorBreaker.Player.Domain;
using FloorBreaker.Player.Application;
using FloorBreaker.Bombs.Domain;
using FloorBreaker.Bombs.Application;

namespace FloorBreaker.Stage.Presentation.Debug
{
    /// <summary>
    /// ステージ Presentation プレビュー用コントローラー。
    /// キー入力でタイル状態を操作し、スプライト・アニメーション・VFX を目視確認する。
    /// </summary>
    public sealed class StagePreviewController : MonoBehaviour
    {
        [Header("Factory (シーンに配置)")]
        [SerializeField] private StageViewFactory _factory;

        [Header("VFX")]
        [SerializeField] private GameObject _fireVfxPrefab;

        [Header("Settings")]
        [SerializeField] private int _stageSize = 30;
        [SerializeField] private float _shrinkAnimDuration = 1.0f;

        [Header("Cursor Visual")]
        [SerializeField] private Color _cursorColor = new Color(1f, 1f, 0f, 0.5f);

        // Domain
        private StageModel _model;
        private TileTimerService _tileTimerService;
        private StageShrinkService _shrinkService;
        private WallGenerationService _wallGenService;
        private StageQueryService _queryService;

        // Bomb simulation (Application 層と同じロジック)
        private BombEffectSpreadService _spreadService;
        private BreakBombResolver _breakResolver;
        private FireBombResolver _fireResolver;

        // Presentation
        private Dictionary<GridPos, TileView> _views;
        private StagePresenter _presenter;
        private StageShrinkAnimator _shrinkAnimator;
        private TileAnimationService _animService;
        private TileFireVfxPool _fireVfxPool;

        // Cursor
        private GridPos _cursorPos;
        private GameObject _cursorIndicator;
        private SpriteRenderer _cursorRenderer;

        // Bomb simulation
        private int _effectRange = 1;

        // Balance defaults
        private const float CollapseDuration = 3f;
        private const float RecoveryDuration = 5f;
        private const float FireDuration = 3.5f;

        private void Start()
        {
            // Domain セットアップ
            var bounds = new TileCoordRange(0, 0, _stageSize - 1, _stageSize - 1);
            _model = new StageModel(bounds);
            _tileTimerService = new TileTimerService(_model);
            _shrinkService = new StageShrinkService();
            _queryService = new StageQueryService(_model);

            // 壁生成
            IRandomProvider random = new SeededRandomProvider(42);
            _wallGenService = new WallGenerationService(0.08f, 0.40f, 0.20f, 2);
            var p1Spawn = new GridPos(2, 2);
            var p2Spawn = new GridPos(_stageSize - 3, _stageSize - 3);
            var walls = _wallGenService.Generate(bounds, p1Spawn, p2Spawn, random);

            foreach (var pos in walls)
            {
                _model.SetTileState(pos, TileState.Wall);
            }

            // Bomb simulation セットアップ
            var safeTileSearch = new SafeTileSearchService();
            var damageService = new PlayerDamageService(1.5f, 1f, _model, safeTileSearch);
            var areaResolver = new BombAreaResolver(_queryService);
            _breakResolver = new BreakBombResolver(areaResolver);
            _fireResolver = new FireBombResolver(areaResolver);
            _spreadService = new BombEffectSpreadService(
                _model, _tileTimerService, damageService, safeTileSearch);

            // Presentation セットアップ
            var config = _factory.Config;
            _views = _factory.CreateTileViews(_model, bounds);
            _animService = new TileAnimationService(config);

            var vfxParent = new GameObject("VfxPool").transform;
            vfxParent.SetParent(transform, false);
            _fireVfxPool = new TileFireVfxPool(_fireVfxPrefab, vfxParent);

            _presenter = new StagePresenter(_model, _views, _animService, _fireVfxPool, config);
            _shrinkAnimator = new StageShrinkAnimator(
                _model, _views, _animService, config, _shrinkAnimDuration);
            _presenter.SetShrinkAnimator(_shrinkAnimator);

            // カーソル初期化
            _cursorPos = new GridPos(_stageSize / 2, _stageSize / 2);
            CreateCursorIndicator();
            UpdateCursorPosition();

            // カメラ
            var cam = Camera.main;
            if (cam != null)
            {
                cam.orthographic = true;
                cam.orthographicSize = _stageSize * 0.55f;
                float center = _stageSize * 0.5f;
                cam.transform.position = new Vector3(center, center, -10f);
            }

            UnityEngine.Debug.Log("[StagePreview] 操作ガイド:");
            UnityEngine.Debug.Log("  矢印キー: カーソル移動");
            UnityEngine.Debug.Log("  1: Normal  |  2: OnFire(単体)  |  3: Collapsing(単体)  |  4: Collapsed");
            UnityEngine.Debug.Log("  5: PermanentlyDestroyed  |  6: Wall");
            UnityEngine.Debug.Log("  F: 炎ボム十字 (壁で停止)  |  C: ブレークボム十字 (壁貫通)");
            UnityEngine.Debug.Log("  +/-: 効果範囲変更 (現在=" + _effectRange + ")");
            UnityEngine.Debug.Log("  S: ステージ縮小  |  R: 全タイルリセット  |  Space: 状態サイクル");
        }

        private void Update()
        {
            // TileTimerService の Tick (崩落→復帰の自動遷移)
            _tileTimerService.Tick(Time.deltaTime);

            // BombEffectSpreadService の Tick (段階的十字広がり)
            _spreadService.Tick(Time.deltaTime);

            // StageShrinkAnimator のバッチフラッシュ
            _shrinkAnimator.FlushPendingWave();

            // カーソル移動
            if (Input.GetKeyDown(KeyCode.UpArrow)) MoveCursor(0, 1);
            if (Input.GetKeyDown(KeyCode.DownArrow)) MoveCursor(0, -1);
            if (Input.GetKeyDown(KeyCode.LeftArrow)) MoveCursor(-1, 0);
            if (Input.GetKeyDown(KeyCode.RightArrow)) MoveCursor(1, 0);

            // タイル状態変更 (単体)
            if (Input.GetKeyDown(KeyCode.Alpha1)) SetCursorTile(TileState.Normal);
            if (Input.GetKeyDown(KeyCode.Alpha2)) SetCursorTileOnFire();
            if (Input.GetKeyDown(KeyCode.Alpha3)) SetCursorTileCollapsing();
            if (Input.GetKeyDown(KeyCode.Alpha4)) SetCursorTile(TileState.Collapsed);
            if (Input.GetKeyDown(KeyCode.Alpha5)) SetCursorTile(TileState.PermanentlyDestroyed);
            if (Input.GetKeyDown(KeyCode.Alpha6)) SetCursorTile(TileState.Wall);

            // 十字パターン (ボムシミュレーション)
            if (Input.GetKeyDown(KeyCode.F)) FireBombCross();
            if (Input.GetKeyDown(KeyCode.C)) BreakBombCross();

            // 効果範囲変更
            if (Input.GetKeyDown(KeyCode.Equals) || Input.GetKeyDown(KeyCode.Plus)
                || Input.GetKeyDown(KeyCode.KeypadPlus))
            {
                _effectRange = Mathf.Min(_effectRange + 1, 10);
                UnityEngine.Debug.Log("[StagePreview] 効果範囲: " + _effectRange);
            }
            if (Input.GetKeyDown(KeyCode.Minus) || Input.GetKeyDown(KeyCode.KeypadMinus))
            {
                _effectRange = Mathf.Max(_effectRange - 1, 1);
                UnityEngine.Debug.Log("[StagePreview] 効果範囲: " + _effectRange);
            }

            // その他
            if (Input.GetKeyDown(KeyCode.S)) ShrinkStage();
            if (Input.GetKeyDown(KeyCode.R)) ResetAllTiles();
            if (Input.GetKeyDown(KeyCode.Space)) CycleState();
        }

        private void MoveCursor(int dx, int dy)
        {
            var newPos = new GridPos(_cursorPos.X + dx, _cursorPos.Y + dy);
            var bounds = _model.GetCurrentBounds();
            if (bounds.Contains(newPos))
            {
                _cursorPos = newPos;
                UpdateCursorPosition();
            }
        }

        private void SetCursorTile(TileState state)
        {
            _model.SetTileState(_cursorPos, state);
        }

        private void SetCursorTileOnFire()
        {
            _model.SetTileState(_cursorPos, TileState.OnFire);
            _tileTimerService.StartFireTimer(_cursorPos, FireDuration);
        }

        private void SetCursorTileCollapsing()
        {
            _model.SetTileState(_cursorPos, TileState.Collapsing);
            _tileTimerService.StartCollapseTimer(_cursorPos, CollapseDuration, RecoveryDuration);
        }

        private const float FireSpreadInterval = 0.15f;
        private const float BreakSpreadInterval = 0.3f;

        /// <summary>
        /// 炎ボムシミュレーション: BombEffectSpreadService 経由の段階的十字広がり。
        /// </summary>
        private void FireBombCross()
        {
            var spec = new BombSpec(BombType.Fire, 3, 3, _effectRange, 1, 2f, false, FireDuration, 0f, 0f);
            var result = _fireResolver.Resolve(_cursorPos, spec, _model);
            _spreadService.EnqueueFireBomb(result, _cursorPos, new List<PlayerModel>(), null, FireSpreadInterval);
            UnityEngine.Debug.Log("[StagePreview] 炎ボム十字 (壁貫通なし, 0.15s/マス): " + result.AffectedTiles.Count + " タイル, 範囲=" + _effectRange);
        }

        /// <summary>
        /// ブレークボムシミュレーション: BombEffectSpreadService 経由の段階的十字広がり。
        /// </summary>
        private void BreakBombCross()
        {
            var spec = new BombSpec(BombType.Break, 3, 3, _effectRange, 2, 4f, true, 0f, CollapseDuration, RecoveryDuration);
            var result = _breakResolver.Resolve(_cursorPos, spec, _model);
            _spreadService.EnqueueBreakBomb(result, _cursorPos, new List<PlayerModel>(), null, BreakSpreadInterval);
            UnityEngine.Debug.Log("[StagePreview] ブレークボム十字 (壁貫通, 0.3s/マス): " + result.AffectedTiles.Count + " タイル, 範囲=" + _effectRange);
        }

        private void ShrinkStage()
        {
            _shrinkService.ShrinkOuterRing(_model);
        }

        private void ResetAllTiles()
        {
            // 全タイマーをキャンセルするために一旦 dispose して再生成
            _tileTimerService.Dispose();
            _tileTimerService = new TileTimerService(_model);

            var bounds = _model.GetCurrentBounds();
            foreach (var pos in bounds.GetAllPositions())
            {
                _model.SetTileState(pos, TileState.Normal);
            }
            _fireVfxPool.DespawnAll();

            // 壁を再生成
            IRandomProvider random = new SeededRandomProvider(42);
            var p1Spawn = new GridPos(2, 2);
            var p2Spawn = new GridPos(_stageSize - 3, _stageSize - 3);
            var walls = _wallGenService.Generate(
                _model.GetCurrentBounds(), p1Spawn, p2Spawn, random);
            foreach (var pos in walls)
            {
                _model.SetTileState(pos, TileState.Wall);
            }

            UnityEngine.Debug.Log("[StagePreview] リセット完了 (壁再生成)");
        }

        private void CycleState()
        {
            var current = _model.GetTileState(_cursorPos);
            var next = current switch
            {
                TileState.Normal => TileState.OnFire,
                TileState.OnFire => TileState.Collapsing,
                TileState.Collapsing => TileState.Collapsed,
                TileState.Collapsed => TileState.PermanentlyDestroyed,
                TileState.PermanentlyDestroyed => TileState.Wall,
                TileState.Wall => TileState.Normal,
                _ => TileState.Normal,
            };

            if (next == TileState.OnFire)
                SetCursorTileOnFire();
            else if (next == TileState.Collapsing)
                SetCursorTileCollapsing();
            else
                SetCursorTile(next);
        }

        private void CreateCursorIndicator()
        {
            _cursorIndicator = new GameObject("CursorIndicator");
            _cursorIndicator.transform.SetParent(transform, false);

            _cursorRenderer = _cursorIndicator.AddComponent<SpriteRenderer>();
            _cursorRenderer.color = _cursorColor;
            _cursorRenderer.sortingOrder = 100;

            // 白い正方形スプライトを生成
            var tex = new Texture2D(4, 4);
            for (int x = 0; x < 4; x++)
                for (int y = 0; y < 4; y++)
                    tex.SetPixel(x, y, Color.white);
            tex.Apply();
            _cursorRenderer.sprite = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
        }

        private void UpdateCursorPosition()
        {
            if (_cursorIndicator == null) return;
            var worldPos = _cursorPos.ToWorldCenter().ToVector3(-0.5f);
            _cursorIndicator.transform.position = worldPos;
        }

        private void OnDestroy()
        {
            _presenter?.Dispose();
            _shrinkAnimator?.Dispose();
            _animService?.Dispose();
            _fireVfxPool?.Dispose();
            _tileTimerService?.Dispose();
            _model?.Dispose();
        }
    }
}
