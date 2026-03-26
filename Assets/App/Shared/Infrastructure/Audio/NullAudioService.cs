using FloorBreaker.Shared.Domain.Primitives;
using FloorBreaker.Shared.Application.Interfaces;

namespace FloorBreaker.Shared.Infrastructure.Audio
{
    /// <summary>
    /// 何もしない IAudioService 実装。AudioService が利用できない場合のフォールバック。
    /// </summary>
    public sealed class NullAudioService : IAudioService
    {
        public void PlaySfx(string sfxId) { }
        public void PlaySfx(string sfxId, Float2 worldPosition) { }
        public void PlayBgm(string bgmId) { }
        public void StopBgm(float fadeOutDuration = 0.5f) { }
    }
}
