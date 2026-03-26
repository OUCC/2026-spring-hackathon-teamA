using System;
using UnityEngine;
using FloorBreaker.Player.Domain;
using FloorBreaker.Stage.Domain;

namespace FloorBreaker.Cameras.Presentation
{
    /// <summary>
    /// 分割画面の 2 台カメラを生成・管理する MonoBehaviour。
    /// Initialize で PlayerModel と StageBounds を受け取り CameraFollower を接続する。
    /// </summary>
    public sealed class SplitScreenCameraSetup : MonoBehaviour, IDisposable
    {
        private const float OrthographicSize = 5f;
        private const float CameraZ = -10f;

        private Camera _cameraP1;
        private Camera _cameraP2;
        private CameraFollower _followerP1;
        private CameraFollower _followerP2;

        public Camera CameraP1 => _cameraP1;
        public Camera CameraP2 => _cameraP2;

        public void Initialize(PlayerModel p1, PlayerModel p2, StageBounds bounds)
        {
            _cameraP1 = CreateCamera("Camera_P1", new Rect(0f, 0f, 0.5f, 1f));
            _cameraP1.gameObject.AddComponent<AudioListener>();
            _cameraP2 = CreateCamera("Camera_P2", new Rect(0.5f, 0f, 0.5f, 1f));

            float viewportAspect = CalculateViewportAspect();

            _followerP1 = new CameraFollower(p1, bounds, OrthographicSize, viewportAspect);
            _followerP2 = new CameraFollower(p2, bounds, OrthographicSize, viewportAspect);

            // 初期位置を即座に反映
            _cameraP1.transform.position = _followerP1.CurrentPosition;
            _cameraP2.transform.position = _followerP2.CurrentPosition;
        }

        /// <summary>
        /// 既存の Camera を使って初期化する (デバッグシーン用)。
        /// </summary>
        public void InitializeWithCameras(
            Camera camP1, Camera camP2,
            PlayerModel p1, PlayerModel p2,
            StageBounds bounds)
        {
            _cameraP1 = camP1;
            _cameraP2 = camP2;

            ConfigureCamera(_cameraP1, new Rect(0f, 0f, 0.5f, 1f));
            ConfigureCamera(_cameraP2, new Rect(0.5f, 0f, 0.5f, 1f));

            float viewportAspect = CalculateViewportAspect();

            _followerP1 = new CameraFollower(p1, bounds, OrthographicSize, viewportAspect);
            _followerP2 = new CameraFollower(p2, bounds, OrthographicSize, viewportAspect);

            _cameraP1.transform.position = _followerP1.CurrentPosition;
            _cameraP2.transform.position = _followerP2.CurrentPosition;
        }

        public void Tick(float deltaTime)
        {
            if (_followerP1 != null)
                _cameraP1.transform.position = _followerP1.Tick(deltaTime);
            if (_followerP2 != null)
                _cameraP2.transform.position = _followerP2.Tick(deltaTime);
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

        private static float CalculateViewportAspect()
        {
            // 各カメラはビューポート幅 0.5 なので、実際の pixel 幅は画面の半分
            float screenWidth = Screen.width * 0.5f;
            float screenHeight = Screen.height;
            return screenHeight > 0 ? screenWidth / screenHeight : 1f;
        }

        public void Dispose()
        {
            _followerP1?.Dispose();
            _followerP2?.Dispose();
        }

        private void OnDestroy()
        {
            Dispose();
        }
    }
}
