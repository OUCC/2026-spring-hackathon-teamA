using System;
using System.Collections.Generic;
using UnityEngine;
using FloorBreaker.Shared.Domain.Primitives;

namespace FloorBreaker.UI.RuntimeUI.Controls
{
    [CreateAssetMenu(fileName = "UpgradeIconMap", menuName = "FloorBreaker/UI/UpgradeIconMap")]
    public sealed class UpgradeIconMap : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public UpgradeId Id;
            public Texture2D Icon;
        }

        [SerializeField] private Entry[] _entries;

        private Dictionary<UpgradeId, Texture2D> _lookup;

        public Texture2D Get(UpgradeId id)
        {
            if (_lookup == null)
            {
                _lookup = new Dictionary<UpgradeId, Texture2D>();
                if (_entries != null)
                    foreach (var e in _entries)
                        _lookup[e.Id] = e.Icon;
            }
            return _lookup.TryGetValue(id, out var tex) ? tex : null;
        }
    }
}
