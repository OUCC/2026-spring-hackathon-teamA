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

        private Tween _zoomTweenP1;
        private Tween _zoomTweenP2;

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
            var camP1 = _cameraSetup?.CameraP1;
            var camP2 = _cameraSetup?.CameraP2;

            if (camP1 != null)
            {
                _zoomTweenP1?.Kill();
                camP1.orthographicSize = BaseOrthoSize;
                _zoomTweenP1 = DOTween.To(
                    () => camP1.orthographicSize,
                    v => camP1.orthographicSize = v,
                    BaseOrthoSize - ZoomPunchAmount,
                    ZoomPunchDuration * 0.4f)
                    .SetEase(Ease.OutQuad)
                    .OnComplete(() =>
                    {
                        DOTween.To(
                            () => camP1.orthographicSize,
                            v => camP1.orthographicSize = v,
                            BaseOrthoSize,
                            ZoomPunchDuration * 0.6f)
                            .SetEase(Ease.InQuad);
                    });
            }

            if (camP2 != null)
            {
                _zoomTweenP2?.Kill();
                camP2.orthographicSize = BaseOrthoSize;
                _zoomTweenP2 = DOTween.To(
                    () => camP2.orthographicSize,
                    v => camP2.orthographicSize = v,
                    BaseOrthoSize - ZoomPunchAmount,
                    ZoomPunchDuration * 0.4f)
                    .SetEase(Ease.OutQuad)
                    .OnComplete(() =>
                    {
                        DOTween.To(
                            () => camP2.orthographicSize,
                            v => camP2.orthographicSize = v,
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
