using UnityEngine;
using DG.Tweening;
using FloorBreaker.Shared.Presentation.Common;

namespace FloorBreaker.Cameras.Presentation
{
    /// <summary>
    /// DOTween ベースのカメラシェイク実装。
    /// SplitScreenCameraSetup の ShakeOffset を揺らすことで、
    /// CameraFollower の位置更新と競合しない。
    /// </summary>
    public sealed class DOTweenCameraShakeService : ICameraShakeService
    {
        private readonly SplitScreenCameraSetup _cameraSetup;

        private Tween _tweenP1;
        private Tween _tweenP2;

        public DOTweenCameraShakeService(SplitScreenCameraSetup cameraSetup)
        {
            _cameraSetup = cameraSetup;
        }

        public void Shake(ShakeIntensity intensity)
        {
            var (duration, strength, vibrato) = GetPreset(intensity);

            // 既存シェイクをキルしてオフセットをリセット
            _tweenP1?.Kill();
            _tweenP2?.Kill();
            _cameraSetup.ShakeOffsetP1 = Vector3.zero;
            _cameraSetup.ShakeOffsetP2 = Vector3.zero;

            // P1 カメラ
            _tweenP1 = DOTween.Shake(
                () => _cameraSetup.ShakeOffsetP1,
                v => _cameraSetup.ShakeOffsetP1 = v,
                duration, strength, vibrato,
                90f, false, true, ShakeRandomnessMode.Harmonic)
                .OnComplete(() => _cameraSetup.ShakeOffsetP1 = Vector3.zero);

            // P2 カメラ
            _tweenP2 = DOTween.Shake(
                () => _cameraSetup.ShakeOffsetP2,
                v => _cameraSetup.ShakeOffsetP2 = v,
                duration, strength, vibrato,
                90f, false, true, ShakeRandomnessMode.Harmonic)
                .OnComplete(() => _cameraSetup.ShakeOffsetP2 = Vector3.zero);
        }

        private static (float duration, float strength, int vibrato) GetPreset(ShakeIntensity intensity)
        {
            return intensity switch
            {
                ShakeIntensity.Light  => (0.10f, 0.08f, 25),
                ShakeIntensity.Medium => (0.20f, 0.20f, 20),
                ShakeIntensity.Heavy  => (0.50f, 0.40f, 15),
                _ => (0.15f, 0.15f, 20),
            };
        }
    }
}
