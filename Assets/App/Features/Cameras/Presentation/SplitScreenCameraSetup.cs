using System;
using System.Collections.Generic;
using UnityEngine;
using FloorBreaker.Player.Domain;
using FloorBreaker.Stage.Domain;
using FloorBreaker.Shared.Application.Interfaces;
using DeviceType = FloorBreaker.Shared.Application.Interfaces.DeviceType;

namespace FloorBreaker.Cameras.Presentation
{
    /// <summary>
    /// N 分割画面のカメラを生成・管理する MonoBehaviour。
    /// Initialize で PlayerModel リストと StageBounds を受け取り CameraFollower を接続する。
    /// </summary>
    public sealed class SplitScreenCameraSetup : MonoBehaviour, IDisposable
    {
        private const float OrthographicSize = 5f;
        private const float CameraZ = -10f;

        private Camera[] _cameras;
        private CameraFollower[] _followers;
        private SpectatorCamera[] _spectators;

        /// <summary>
        /// 画面シェイク用オフセット。DOTweenCameraShakeService から書き込まれる。
        /// </summary>
        public Vector3[] ShakeOffsets { get; private set; }

        public Camera[] Cameras => _cameras;
        public int CameraCount => _cameras?.Length ?? 0;
        public SpectatorCamera[] Spectators => _spectators;

        /// <summary>Viewport レイアウトテーブル。</summary>
        private static readonly Rect[][] ViewportTable =
        {
            // 1P: フルスクリーン
            new[] { new Rect(0f, 0f, 1f, 1f) },
            // 2P: 左右分割
            new[] { new Rect(0f, 0f, 0.5f, 1f), new Rect(0.5f, 0f, 0.5f, 1f) },
            // 3P: 上に2つ + 下に1つ（中央寄せ）
            new[] { new Rect(0f, 0.5f, 0.5f, 0.5f), new Rect(0.5f, 0.5f, 0.5f, 0.5f), new Rect(0.25f, 0f, 0.5f, 0.5f) },
            // 4P: 4象限
            new[] { new Rect(0f, 0.5f, 0.5f, 0.5f), new Rect(0.5f, 0.5f, 0.5f, 0.5f), new Rect(0f, 0f, 0.5f, 0.5f), new Rect(0.5f, 0f, 0.5f, 0.5f) },
        };

        public void Initialize(IReadOnlyList<PlayerModel> players, StageBounds bounds)
        {
            int count = players.Count;
            var rects = ViewportTable[count - 1];

            _cameras = new Camera[count];
            _followers = new CameraFollower[count];
            ShakeOffsets = new Vector3[count];

            for (int i = 0; i < count; i++)
            {
                _cameras[i] = CreateCamera($"Camera_P{i + 1}", rects[i]);
                if (i == 0) _cameras[i].gameObject.AddComponent<AudioListener>();

                float viewportAspect = CalculateViewportAspect(rects[i]);
                _followers[i] = new CameraFollower(players[i], bounds, OrthographicSize, viewportAspect);
                _cameras[i].transform.position = _followers[i].CurrentPosition;
            }
        }

        /// <summary>
        /// 既存の Camera を使って初期化する (デバッグシーン用)。
        /// </summary>
        public void InitializeWithCameras(
            Camera camP1, Camera camP2,
            PlayerModel p1, PlayerModel p2,
            StageBounds bounds)
        {
            _cameras = new[] { camP1, camP2 };
            _followers = new CameraFollower[2];
            ShakeOffsets = new Vector3[2];

            var rects = ViewportTable[1]; // 2P
            ConfigureCamera(camP1, rects[0]);
            ConfigureCamera(camP2, rects[1]);

            float viewportAspect = CalculateViewportAspect(rects[0]);
            _followers[0] = new CameraFollower(p1, bounds, OrthographicSize, viewportAspect);
            _followers[1] = new CameraFollower(p2, bounds, OrthographicSize, viewportAspect);

            camP1.transform.position = _followers[0].CurrentPosition;
            camP2.transform.position = _followers[1].CurrentPosition;
        }

        /// <summary>
        /// 全員 CPU 時の観戦カメラを生成する。フルスクリーン1台。
        /// </summary>
        public void InitializeSpectator(StageBounds bounds, IReadOnlyList<PlayerModel> allPlayers)
        {
            _cameras = new Camera[1];
            _followers = new CameraFollower[1]; // null のまま
            _spectators = new SpectatorCamera[1];
            ShakeOffsets = new Vector3[1];

            _cameras[0] = CreateCamera("Camera_Spectator", new Rect(0f, 0f, 1f, 1f));
            _cameras[0].gameObject.AddComponent<AudioListener>();
            _spectators[0] = new SpectatorCamera(_cameras[0], bounds, allPlayers);
        }

        /// <summary>
        /// 死亡した Human プレイヤーのカメラを観戦モードに変換する。
        /// </summary>
        public void ConvertToSpectator(
            int cameraIndex, StageBounds bounds, IReadOnlyList<PlayerModel> allPlayers,
            DeviceType deviceType = DeviceType.None, int gamepadIndex = -1)
        {
            if (_cameras == null || cameraIndex < 0 || cameraIndex >= _cameras.Length) return;
            if (_spectators == null) _spectators = new SpectatorCamera[_cameras.Length];

            // CameraFollower を破棄して SpectatorCamera に切り替え
            _followers[cameraIndex]?.Dispose();
            _followers[cameraIndex] = null;
            _spectators[cameraIndex] = new SpectatorCamera(
                _cameras[cameraIndex], bounds, allPlayers, deviceType, gamepadIndex);
        }

        public void Tick(float deltaTime)
        {
            if (_cameras == null) return;
            for (int i = 0; i < _cameras.Length; i++)
            {
                if (_spectators != null && _spectators.Length > i && _spectators[i] != null)
                {
                    _spectators[i].Tick(deltaTime);
                }
                else if (_followers != null && _followers.Length > i && _followers[i] != null)
                {
                    _cameras[i].transform.position = _followers[i].Tick(deltaTime) + ShakeOffsets[i];
                }
            }
        }

        private Camera CreateCamera(string cameraName, Rect viewportRect)
        {
            var go = new GameObject(cameraName);
            go.transform.SetParent(transform);
            var cam = go.AddComponent<Camera>();
            ConfigureCamera(cam, viewportRect);
            return cam;
        }

        private static void ConfigureCamera(Camera cam, Rect viewportRect)
        {
            cam.orthographic = true;
            cam.orthographicSize = OrthographicSize;
            cam.rect = viewportRect;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 100f;
            cam.backgroundColor = Color.black;
            cam.clearFlags = CameraClearFlags.SolidColor;

            var pos = cam.transform.position;
            pos.z = CameraZ;
            cam.transform.position = pos;
        }

        private static float CalculateViewportAspect(Rect viewportRect)
        {
            float screenWidth = Screen.width * viewportRect.width;
            float screenHeight = Screen.height * viewportRect.height;
            return screenHeight > 0 ? screenWidth / screenHeight : 1f;
        }

        public void Dispose()
        {
            if (_followers == null) return;
            foreach (var f in _followers) f?.Dispose();
        }

        private void OnDestroy()
        {
            Dispose();
        }
    }
}
