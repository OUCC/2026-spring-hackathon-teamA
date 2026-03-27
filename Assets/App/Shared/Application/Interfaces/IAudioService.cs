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

        /// <summary>BGM を一時的にダッキングする (非永続)。</summary>
        void DuckBgm(float volume, float fadeDuration = 0.3f);

        // --- 音量設定 ---

        /// <summary>マスター音量 (0-1)。</summary>
        float MasterVolume { get; }

        /// <summary>BGM 音量 (0-1)。</summary>
        float BgmVolume { get; }

        /// <summary>SE 音量 (0-1)。</summary>
        float SfxVolume { get; }

        /// <summary>マスター音量を設定して永続化する。</summary>
        void SetMasterVolume(float volume);

        /// <summary>BGM 音量レベルを設定して永続化する。</summary>
        void SetBgmVolumeLevel(float volume);

        /// <summary>SE 音量を設定して永続化する。</summary>
        void SetSfxVolume(float volume);
    }
}
