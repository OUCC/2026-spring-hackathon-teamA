using System;
using System.Collections.Generic;
using UnityEngine;

namespace FloorBreaker.Shared.Infrastructure.Audio
{
    /// <summary>
    /// sfxId → AudioClip + volume のマッピングを管理する ScriptableObject。
    /// </summary>
    [CreateAssetMenu(fileName = "AudioCatalog", menuName = "FloorBreaker/Audio Catalog")]
    public sealed class AudioCatalog : ScriptableObject
    {
        [SerializeField] private AudioEntry[] _entries = Array.Empty<AudioEntry>();

        private Dictionary<string, AudioEntry> _lookup;

        private void OnEnable()
        {
            RebuildLookup();
        }

        public bool TryGetEntry(string sfxId, out AudioClip clip, out float volume)
        {
            if (_lookup == null) RebuildLookup();

            if (_lookup.TryGetValue(sfxId, out var entry))
            {
                clip = entry.Clip;
                volume = entry.Volume;
                return clip != null;
            }

            clip = null;
            volume = 0f;
            return false;
        }

        private void RebuildLookup()
        {
            _lookup = new Dictionary<string, AudioEntry>(_entries.Length);
            foreach (var entry in _entries)
            {
                if (!string.IsNullOrEmpty(entry.SfxId))
                    _lookup[entry.SfxId] = entry;
            }
        }

        [Serializable]
        public struct AudioEntry
        {
            [SerializeField] private string _sfxId;
            [SerializeField] private AudioClip _clip;
            [SerializeField, Range(0f, 2f)] private float _volume;

            public string SfxId => _sfxId;
            public AudioClip Clip => _clip;
            public float Volume => _volume > 0f ? _volume : 1f;

            public AudioEntry(string sfxId, AudioClip clip, float volume = 1f)
            {
                _sfxId = sfxId;
                _clip = clip;
                _volume = volume;
            }
        }
    }
}
