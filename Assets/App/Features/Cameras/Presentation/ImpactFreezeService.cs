using UnityEngine;
using UnityEngine.UIElements;
using DG.Tweening;
using FloorBreaker.Shared.Presentation.Common;

namespace FloorBreaker.Cameras.Presentation
{
    /// <summary>
    /// ネットワーク安全なヒットストップ演出。
    /// Time.timeScale を操作せず、カメラズームパンチ + UI フラッシュで表現する。
    /// </summary>
    public sealed class ImpactFreezeService : IImpactFreezeService
    {
        private const float BaseOrthoSize = 5f;
        private const float ZoomPunchAmount = 0.15f;
        private const float ZoomPunchDuration = 0.12f;

        private readonly SplitScreenCameraSetup _cameraSetup;
        private VisualElement _flashOverlay;

        private Tween[] _zoomTweens;

        public ImpactFreezeService(SplitScreenCameraSetup cameraSetup)
        {
            _cameraSetup = cameraSetup;
        }

        /// <summary>
        /// UI フラッシュ用の VisualElement を設定する。
        /// MatchInitializer から呼ばれる。
        /// </summary>
        public void SetFlashOverlay(VisualElement flashOverlay)
        {
            _flashOverlay = flashOverlay;
        }

        public void PlayImpact(ImpactLevel level)
        {
            switch (level)
            {
                case ImpactLevel.Light:
                    break;
                case ImpactLevel.Medium:
                    PlayZoomPunch();
                    break;
                case ImpactLevel.Heavy:
                    PlayZoomPunch();
                    PlayFlash();
                    break;
            }
        }

        private void PlayZoomPunch()
        {
            var cameras = _cameraSetup?.Cameras;
            if (cameras == null) return;

            if (_zoomTweens == null || _zoomTweens.Length != cameras.Length)
                _zoomTweens = new Tween[cameras.Length];

            for (int i = 0; i < cameras.Length; i++)
            {
                var cam = cameras[i];
                if (cam == null) continue;

                _zoomTweens[i]?.Kill();
                cam.orthographicSize = BaseOrthoSize;
                var capturedCam = cam;
                var capturedIndex = i;
                _zoomTweens[i] = DOTween.To(
                    () => capturedCam.orthographicSize,
                    v => capturedCam.orthographicSize = v,
                    BaseOrthoSize - ZoomPunchAmount,
                    ZoomPunchDuration * 0.4f)
                    .SetEase(Ease.OutQuad)
                    .OnComplete(() =>
                    {
                        DOTween.To(
                            () => capturedCam.orthographicSize,
                            v => capturedCam.orthographicSize = v,
                            BaseOrthoSize,
                            ZoomPunchDuration * 0.6f)
                            .SetEase(Ease.InQuad);
                    });
            }
        }

        private void PlayFlash()
        {
            if (_flashOverlay == null) return;

            _flashOverlay.AddToClassList("impact-flash--active");
            _flashOverlay.schedule.Execute(() =>
                _flashOverlay.RemoveFromClassList("impact-flash--active"));
        }
    }
}
