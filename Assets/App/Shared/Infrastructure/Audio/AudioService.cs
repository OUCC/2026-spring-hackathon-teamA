using System.Collections;
using UnityEngine;
using FloorBreaker.Shared.Domain.Primitives;
using FloorBreaker.Shared.Application.Interfaces;

namespace FloorBreaker.Shared.Infrastructure.Audio
{
    /// <summary>
    /// IAudioService の実装。SFX プール + BGM ループ再生。
    /// ProjectLifetimeScope に Singleton 登録し、DontDestroyOnLoad で生存する。
    /// </summary>
    public sealed class AudioService : MonoBehaviour, IAudioService
    {
        [SerializeField] private AudioCatalog _catalog;
        [SerializeField, Range(0f, 1f)] private float _masterVolume = 0.5f;
        [SerializeField, Range(0f, 1f)] private float _bgmVolume = 0.5f;
        [SerializeField, Range(0f, 1f)] private float _sfxVolume = 0.5f;

        private const int InitialPoolSize = 4;
        private const int MaxPoolSize = 8;
        private const float StageWidth = 30f;
        private const float MaxPanStrength = 0.3f;

        private const string PrefsMasterVolume = "AudioMasterVolume";
        private const string PrefsBgmVolume = "AudioBgmVolume";
        private const string PrefsSfxVolume = "AudioSfxVolume";

        private AudioSource[] _pool;
        private int _nextIndex;

        // BGM 専用
        private AudioSource _bgmSource;
        private Coroutine _bgmFadeCoroutine;
        private string _currentBgmId;

        // --- 音量プロパティ ---
        public float MasterVolume => _masterVolume;
        public float BgmVolume => _bgmVolume;
        public float SfxVolume => _sfxVolume;

        private void Awake()
        {
            // PlayerPrefs から音量をロード（保存済みなら上書き）
            if (PlayerPrefs.HasKey(PrefsMasterVolume))
                _masterVolume = PlayerPrefs.GetFloat(PrefsMasterVolume);
            if (PlayerPrefs.HasKey(PrefsBgmVolume))
                _bgmVolume = PlayerPrefs.GetFloat(PrefsBgmVolume);
            if (PlayerPrefs.HasKey(PrefsSfxVolume))
                _sfxVolume = PlayerPrefs.GetFloat(PrefsSfxVolume);

            _pool = new AudioSource[MaxPoolSize];
            for (int i = 0; i < InitialPoolSize; i++)
            {
                _pool[i] = CreateAudioSource();
            }

            _bgmSource = gameObject.AddComponent<AudioSource>();
            _bgmSource.playOnAwake = false;
            _bgmSource.spatialBlend = 0f;
            _bgmSource.loop = true;
            _bgmSource.volume = _bgmVolume * _masterVolume;
        }

        // === 音量設定 ===

        public void SetMasterVolume(float volume)
        {
            _masterVolume = Mathf.Clamp01(volume);
            PlayerPrefs.SetFloat(PrefsMasterVolume, _masterVolume);
            PlayerPrefs.Save();
            ApplyBgmVolume();
        }

        public void SetBgmVolumeLevel(float volume)
        {
            _bgmVolume = Mathf.Clamp01(volume);
            PlayerPrefs.SetFloat(PrefsBgmVolume, _bgmVolume);
            PlayerPrefs.Save();
            ApplyBgmVolume();
        }

        public void SetSfxVolume(float volume)
        {
            _sfxVolume = Mathf.Clamp01(volume);
            PlayerPrefs.SetFloat(PrefsSfxVolume, _sfxVolume);
            PlayerPrefs.Save();
        }

        private void ApplyBgmVolume()
        {
            if (_bgmSource == null || !_bgmSource.isPlaying) return;

            float baseVolume = 1f;
            if (_currentBgmId != null && _catalog != null &&
                _catalog.TryGetEntry(_currentBgmId, out _, out var catVol))
            {
                baseVolume = catVol;
            }
            _bgmSource.volume = baseVolume * _bgmVolume * _masterVolume;
        }

        // === SFX ===

        public void PlaySfx(string sfxId)
        {
            if (_catalog == null) return;
            if (!_catalog.TryGetEntry(sfxId, out var clip, out var volume)) return;

            var source = GetAvailableSource();
            source.panStereo = 0f;
            source.volume = volume * _sfxVolume * _masterVolume;
            source.clip = clip;
            source.Play();
        }

        public void PlaySfx(string sfxId, Float2 worldPosition)
        {
            if (_catalog == null) return;
            if (!_catalog.TryGetEntry(sfxId, out var clip, out var volume)) return;

            var source = GetAvailableSource();
            float rawPan = Mathf.Clamp((worldPosition.X - StageWidth * 0.5f) / (StageWidth * 0.5f), -1f, 1f);
            source.panStereo = rawPan * MaxPanStrength;
            source.volume = volume * _sfxVolume * _masterVolume;
            source.clip = clip;
            source.Play();
        }

        // === BGM ===

        public void PlayBgm(string bgmId)
        {
            if (_catalog == null) return;
            if (bgmId == _currentBgmId && _bgmSource.isPlaying) return;
            if (!_catalog.TryGetEntry(bgmId, out var clip, out var volume)) return;

            // フェード中なら停止
            if (_bgmFadeCoroutine != null)
            {
                StopCoroutine(_bgmFadeCoroutine);
                _bgmFadeCoroutine = null;
            }

            _currentBgmId = bgmId;
            _bgmSource.clip = clip;
            _bgmSource.volume = volume * _bgmVolume * _masterVolume;
            _bgmSource.Play();
        }

        public void StopBgm(float fadeOutDuration = 0.5f)
        {
            if (!_bgmSource.isPlaying) return;

            if (fadeOutDuration <= 0f)
            {
                _bgmSource.Stop();
                _currentBgmId = null;
                return;
            }

            if (_bgmFadeCoroutine != null)
                StopCoroutine(_bgmFadeCoroutine);

            _bgmFadeCoroutine = StartCoroutine(FadeOutBgm(fadeOutDuration));
        }

        public void SetBgmVolume(float volume, float fadeDuration = 0.3f)
        {
            if (!_bgmSource.isPlaying) return;

            // カタログからベース音量を取得
            float baseVolume = 1f;
            if (_currentBgmId != null && _catalog != null &&
                _catalog.TryGetEntry(_currentBgmId, out _, out var catVol))
            {
                baseVolume = catVol;
            }

            float targetVolume = baseVolume * volume * _bgmVolume * _masterVolume;

            if (fadeDuration <= 0f)
            {
                _bgmSource.volume = targetVolume;
                return;
            }

            if (_bgmFadeCoroutine != null)
                StopCoroutine(_bgmFadeCoroutine);

            _bgmFadeCoroutine = StartCoroutine(FadeBgmVolume(targetVolume, fadeDuration));
        }

        private IEnumerator FadeBgmVolume(float targetVolume, float duration)
        {
            float startVolume = _bgmSource.volume;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                _bgmSource.volume = Mathf.Lerp(startVolume, targetVolume, elapsed / duration);
                yield return null;
            }

            _bgmSource.volume = targetVolume;
            _bgmFadeCoroutine = null;
        }

        private IEnumerator FadeOutBgm(float duration)
        {
            float startVolume = _bgmSource.volume;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                _bgmSource.volume = Mathf.Lerp(startVolume, 0f, elapsed / duration);
                yield return null;
            }

            _bgmSource.Stop();
            _bgmSource.volume = startVolume;
            _currentBgmId = null;
            _bgmFadeCoroutine = null;
        }

        // === Pool ===

        private AudioSource GetAvailableSource()
        {
            for (int i = 0; i < _pool.Length; i++)
            {
                if (_pool[i] != null && !_pool[i].isPlaying)
                    return _pool[i];
            }

            for (int i = 0; i < MaxPoolSize; i++)
            {
                if (_pool[i] == null)
                {
                    _pool[i] = CreateAudioSource();
                    return _pool[i];
                }
            }

            var source = _pool[_nextIndex];
            _nextIndex = (_nextIndex + 1) % MaxPoolSize;
            return source;
        }

        private AudioSource CreateAudioSource()
        {
            var source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            return source;
        }
    }
}
