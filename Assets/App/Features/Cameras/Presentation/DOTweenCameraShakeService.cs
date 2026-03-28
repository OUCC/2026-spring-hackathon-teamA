using UnityEngine;
using DG.Tweening;
using FloorBreaker.Shared.Presentation.Common;

namespace FloorBreaker.Cameras.Presentation
{
    /// <summary>
    /// DOTween ベースのカメラシェイク実装。
    /// SplitScreenCameraSetup の ShakeOffsets を揺らすことで、
    /// CameraFollower の位置更新と競合しない。
    /// N カメラ対応。
    /// </summary>
    public sealed class DOTweenCameraShakeService : ICameraShakeService
    {
        private readonly SplitScreenCameraSetup _cameraSetup;
        private Tween[] _tweens;

        public DOTweenCameraShakeService(SplitScreenCameraSetup cameraSetup)
        {
            _cameraSetup = cameraSetup;
        }

        public void Shake(ShakeIntensity intensity)
        {
            var offsets = _cameraSetup.ShakeOffsets;
            if (offsets == null || offsets.Length == 0) return;

            var (duration, strength, vibrato) = GetPreset(intensity);

            // 既存シェイクをキルしてオフセットをリセット
            if (_tweens != null)
            {
                for (int i = 0; i < _tweens.Length; i++)
                    _tweens[i]?.Kill();
            }

            _tweens = new Tween[offsets.Length];

            for (int i = 0; i < offsets.Length; i++)
            {
                offsets[i] = Vector3.zero;
                int idx = i; // capture for closure

                _tweens[i] = DOTween.Shake(
                    () => _cameraSetup.ShakeOffsets[idx],
                    v => _cameraSetup.ShakeOffsets[idx] = v,
                    duration, strength, vibrato,
                    90f, false, true, ShakeRandomnessMode.Harmonic)
                    .OnComplete(() => _cameraSetup.ShakeOffsets[idx] = Vector3.zero);
            }
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
