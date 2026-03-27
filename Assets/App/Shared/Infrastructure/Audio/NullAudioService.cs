using FloorBreaker.Shared.Domain.Primitives;
using FloorBreaker.Shared.Application.Interfaces;

namespace FloorBreaker.Shared.Infrastructure.Audio
{
    /// <summary>
    /// 何もしない IAudioService 実装。AudioService が利用できない場合のフォールバック。
    /// </summary>
    public sealed class NullAudioService : IAudioService
    {
        public float MasterVolume => 0f;
        public float BgmVolume => 0f;
        public float SfxVolume => 0f;

        public void PlaySfx(string sfxId) { }
        public void PlaySfx(string sfxId, Float2 worldPosition) { }
        public void PlayBgm(string bgmId) { }
        public void StopBgm(float fadeOutDuration = 0.5f) { }
        public void DuckBgm(float volume, float fadeDuration = 0.3f) { }
        public void SetMasterVolume(float volume) { }
        public void SetBgmVolumeLevel(float volume) { }
        public void SetSfxVolume(float volume) { }
    }
}
