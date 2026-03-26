using UnityEngine;
using FloorBreaker.Shared.Domain.Primitives;
using FloorBreaker.Shared.Application.Interfaces;

namespace FloorBreaker.Shared.Infrastructure.Audio
{
    /// <summary>
    /// IAudioService の実装。AudioSource プールで複数 SE 同時再生。
    /// ProjectLifetimeScope に Singleton 登録し、DontDestroyOnLoad で生存する。
    /// </summary>
    public sealed class AudioService : MonoBehaviour, IAudioService
    {
        [SerializeField] private AudioCatalog _catalog;
        [SerializeField, Range(0f, 1f)] private float _masterVolume = 1f;

        private const int InitialPoolSize = 4;
        private const int MaxPoolSize = 8;
        private const float StageWidth = 30f;

        private AudioSource[] _pool;
        private int _nextIndex;

        private void Awake()
        {
            _pool = new AudioSource[MaxPoolSize];
            for (int i = 0; i < InitialPoolSize; i++)
            {
                _pool[i] = CreateAudioSource();
            }
        }

        public void PlaySfx(string sfxId)
        {
            if (_catalog == null) return;
            if (!_catalog.TryGetEntry(sfxId, out var clip, out var volume)) return;

            var source = GetAvailableSource();
            source.panStereo = 0f;
            source.volume = volume * _masterVolume;
            source.clip = clip;
            source.Play();
        }

        public void PlaySfx(string sfxId, Float2 worldPosition)
        {
            if (_catalog == null) return;
            if (!_catalog.TryGetEntry(sfxId, out var clip, out var volume)) return;

            var source = GetAvailableSource();
            // ステージ中央を 0、左端を -1、右端を +1 にマッピング
            float pan = Mathf.Clamp((worldPosition.X - StageWidth * 0.5f) / (StageWidth * 0.5f), -1f, 1f);
            source.panStereo = pan;
            source.volume = volume * _masterVolume;
            source.clip = clip;
            source.Play();
        }

        private AudioSource GetAvailableSource()
        {
            // 再生中でないソースを探す
            for (int i = 0; i < _pool.Length; i++)
            {
                if (_pool[i] != null && !_pool[i].isPlaying)
                    return _pool[i];
            }

            // 全て再生中 → プールに空きがあれば新規作成
            for (int i = 0; i < MaxPoolSize; i++)
            {
                if (_pool[i] == null)
                {
                    _pool[i] = CreateAudioSource();
                    return _pool[i];
                }
            }

            // 全て埋まっている → ラウンドロビンで上書き
            var source = _pool[_nextIndex];
            _nextIndex = (_nextIndex + 1) % MaxPoolSize;
            return source;
        }

        private AudioSource CreateAudioSource()
        {
            var source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f; // 2D
            return source;
        }
    }
}
