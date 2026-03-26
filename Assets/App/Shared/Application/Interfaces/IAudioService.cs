using FloorBreaker.Shared.Domain.Primitives;

namespace FloorBreaker.Shared.Application.Interfaces
{
    public interface IAudioService
    {
        void PlaySfx(string sfxId);
        void PlaySfx(string sfxId, Float2 worldPosition);

        /// <summary>BGM をループ再生する。既に再生中なら切り替える。</summary>
        void PlayBgm(string bgmId);

        /// <summary>BGM をフェードアウトして停止する。</summary>
        void StopBgm(float fadeOutDuration = 0.5f);
    }
}
