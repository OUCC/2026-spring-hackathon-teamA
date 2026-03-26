using System.Collections.Generic;
using UnityEngine;
using FloorBreaker.Shared.Domain.Grid;
using FloorBreaker.Shared.Domain.Primitives;
using FloorBreaker.Shared.Application.Interfaces;
using FloorBreaker.Shared.Infrastructure.Random;
using FloorBreaker.Shared.Presentation.Common;
using FloorBreaker.Stage.Domain;
using FloorBreaker.Player.Domain;

namespace FloorBreaker.Cameras.Presentation.Debug
{
    /// <summary>
    /// カメラ Presentation プレビュー用コントローラー。
    /// WASD / 矢印キーで P1/P2 を操作し、分割画面カメラの追従・クランプを目視確認する。
    /// DI なし、手動配線。
    /// </summary>
    public sealed class CameraPreviewController : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private int _stageSize = 30;

        // Domain
        private StageModel _stageModel;
        private StageBounds _stageBounds;
        private WallGenerationService _wallGenService;
        private PlayerMoveService _moveService;
        private PlayerModel _player1;
        private PlayerModel _player2;

        // Presentation — Camera
        private SplitScreenCameraSetup _cameraSetup;

        // Presentation — Player markers
        private GameObject _p1Marker;
        private GameObject _p2Marker;

        // Input state
        private float _p1MoveTimer;
        private float _p2MoveTimer;
        private const float MoveRepeatRate = 0.08f;
        private const float MoveFirstDelay = 0.01f;

        private void Start()
        {
            SetupStage();
            SetupPlayers();
            SetupPlayerMarkers();
            SetupCameras();
            DrawGrid();
            LogControls();
        }

        private void SetupStage()
        {
            var bounds = new TileCoordRange(0, 0, _stageSize - 1, _stageSize - 1);
            _stageModel = new StageModel(bounds);
            _stageBounds = _stageModel.Bounds;

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
        }

        private void SetupPlayers()
        {
            _moveService = new PlayerMoveService();

            var p1Spawn = new GridPos(2, 2);
            var p2Spawn = new GridPos(_stageSize - 3, _stageSize - 3);

            _player1 = CreatePlayerModel(PlayerId.Player1, p1Spawn);
            _player2 = CreatePlayerModel(PlayerId.Player2, p2Spawn);
        }

        private PlayerModel CreatePlayerModel(PlayerId id, GridPos spawn)
        {
            var stats = new PlayerStats(10, 1f, 3f);
            var build = new PlayerBuild(3, 1, 1, 2f, 3.5f, false, 0.5f, 3, 1, 2, 4f, 3f, 1f);
            return new PlayerModel(id, spawn, stats, build);
        }

        private void SetupPlayerMarkers()
        {
            _p1Marker = CreateMarker("P1_Marker", new Color(0.29f, 0.56f, 0.85f, 1f));
            _p2Marker = CreateMarker("P2_Marker", new Color(0.85f, 0.29f, 0.29f, 1f));
            UpdateMarkerPositions();
        }

        private GameObject CreateMarker(string markerName, Color color)
        {
            var go = new GameObject(markerName);
            go.transform.SetParent(transform, false);

            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.color = color;
            renderer.sortingOrder = 10;

            var tex = new Texture2D(4, 4);
            for (int x = 0; x < 4; x++)
                for (int y = 0; y < 4; y++)
                    tex.SetPixel(x, y, Color.white);
            tex.Apply();
            renderer.sprite = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
            go.transform.localScale = new Vector3(0.8f, 0.8f, 1f);

            return go;
        }

        private void SetupCameras()
        {
            // 既存の MainCamera を無効化
            var mainCam = Camera.main;
            if (mainCam != null)
            {
                mainCam.enabled = false;
            }

            var setupGo = new GameObject("SplitScreenCameraSetup");
            setupGo.transform.SetParent(transform, false);
            _cameraSetup = setupGo.AddComponent<SplitScreenCameraSetup>();
            _cameraSetup.Initialize(_player1, _player2, _stageBounds);
        }

        /// <summary>
        /// グリッド境界を視覚化するための簡易描画。
        /// </summary>
        private void DrawGrid()
        {
            var gridParent = new GameObject("Grid").transform;
            gridParent.SetParent(transform, false);

            var range = _stageBounds.Current;

            // 壁タイルを描画
            for (int y = range.MinY; y <= range.MaxY; y++)
            {
                for (int x = range.MinX; x <= range.MaxX; x++)
                {
                    var pos = new GridPos(x, y);
                    var state = _stageModel.GetTileState(pos);

                    var go = new GameObject($"Tile_{x}_{y}");
                    go.transform.SetParent(gridParent, false);
                    go.transform.position = pos.ToWorldCenter().ToVector3(0f);

                    var renderer = go.AddComponent<SpriteRenderer>();
                    var tex = new Texture2D(4, 4);
                    for (int px = 0; px < 4; px++)
                        for (int py = 0; py < 4; py++)
                            tex.SetPixel(px, py, Color.white);
                    tex.Apply();
                    renderer.sprite = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);

                    if (state == TileState.Wall)
                    {
                        renderer.color = new Color(0.3f, 0.25f, 0.2f, 1f);
                    }
                    else
                    {
                        renderer.color = new Color(0.85f, 0.82f, 0.75f, 1f);
                    }
                    renderer.sortingOrder = -1;
                }
            }
        }

        private void Update()
        {
            float dt = Time.deltaTime;

            HandleP1Input(dt);
            HandleP2Input(dt);

            _cameraSetup.Tick(dt);

            UpdateMarkerPositions();

            // ステージ縮小
            if (Input.GetKeyDown(KeyCode.Space))
            {
                var shrinkService = new StageShrinkService();
                shrinkService.ShrinkOuterRing(_stageModel);
                UnityEngine.Debug.Log($"[CameraPreview] ステージ縮小 → {_stageBounds.Current}");
            }
        }

        private void UpdateMarkerPositions()
        {
            if (_p1Marker != null)
                _p1Marker.transform.position = _player1.CurrentPosition.ToWorldCenter().ToVector3(-1f);
            if (_p2Marker != null)
                _p2Marker.transform.position = _player2.CurrentPosition.ToWorldCenter().ToVector3(-1f);
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

        private static Direction8? ReadWASD()
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

        private static Direction8? ReadArrows()
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

        private void LogControls()
        {
            UnityEngine.Debug.Log("[CameraPreview] 操作ガイド:");
            UnityEngine.Debug.Log("  W/A/S/D: P1 移動 (青マーカー、左画面)");
            UnityEngine.Debug.Log("  矢印キー: P2 移動 (赤マーカー、右画面)");
            UnityEngine.Debug.Log("  Space: ステージ縮小 (カメラクランプ確認)");
        }

        private void OnDestroy()
        {
            _cameraSetup?.Dispose();
            _player1?.Dispose();
            _player2?.Dispose();
            _stageModel?.Dispose();
        }
    }
}
