using System;
using System.Collections.Generic;
using UnityEngine;
using FloorBreaker.Shared.Domain.Grid;
using FloorBreaker.Shared.Presentation.Common;

namespace FloorBreaker.Stage.Presentation
{
    public sealed class TileFireVfxPool : IDisposable
    {
        private static readonly Vector3 VfxOffset = new(0f, 0.1f, -0.1f);
        private static readonly Vector3 VfxScale = new(0.24f, 0.24f, 0.24f);

        private readonly GameObject _prefab;
        private readonly Transform _poolParent;
        private readonly int _overrideLayer;
        private readonly Stack<GameObject> _available = new();
        private readonly Dictionary<GridPos, GameObject> _active = new();

        /// <param name="overrideLayer">-1 の場合レイヤー変更なし。0以上でプール内全 GO のレイヤーを上書き。</param>
        public TileFireVfxPool(GameObject prefab, Transform poolParent, int initialCapacity = 20, int overrideLayer = -1)
        {
            _prefab = prefab;
            _poolParent = poolParent;
            _overrideLayer = overrideLayer;

            if (_prefab == null) return;

            for (int i = 0; i < initialCapacity; i++)
            {
                var go = UnityEngine.Object.Instantiate(_prefab, _poolParent);
                if (_overrideLayer >= 0) go.SetLayerRecursive(_overrideLayer);
                go.SetActive(false);
                _available.Push(go);
            }
        }

        public void SpawnAt(GridPos pos, Vector3 worldPos, Color? tint = null)
        {
            if (_prefab == null) return;
            if (_active.ContainsKey(pos)) return;

            GameObject go;
            if (_available.Count > 0)
            {
                go = _available.Pop();
            }
            else
            {
                go = UnityEngine.Object.Instantiate(_prefab, _poolParent);
                if (_overrideLayer >= 0) go.SetLayerRecursive(_overrideLayer);
            }

            go.transform.position = worldPos + VfxOffset;
            go.transform.localScale = VfxScale;
            go.SetActive(true);

            // パーティクルリセット + 色変更
            var ps = go.GetComponentInChildren<ParticleSystem>();
            if (ps != null)
            {
                if (tint.HasValue)
                {
                    var main = ps.main;
                    main.startColor = tint.Value;
                }
                ps.Clear();
                ps.Play();
            }

            _active[pos] = go;
        }

        /// <summary>
        /// アクティブな炎 VFX のスケールを更新する（残り時間に応じた減衰表現）。
        /// </summary>
        public void SetScale(GridPos pos, float scale)
        {
            if (!_active.TryGetValue(pos, out var go)) return;
            float s = scale * VfxScale.x;
            go.transform.localScale = new Vector3(s, s, s);
        }

        public void DespawnAt(GridPos pos)
        {
            if (!_active.TryGetValue(pos, out var go)) return;

            var ps = go.GetComponentInChildren<ParticleSystem>();
            if (ps != null) ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            go.SetActive(false);
            _available.Push(go);
            _active.Remove(pos);
        }

        public void DespawnAll()
        {
            var positions = new List<GridPos>(_active.Keys);
            foreach (var pos in positions)
            {
                DespawnAt(pos);
            }
        }

        public void Dispose()
        {
            DespawnAll();
            while (_available.Count > 0)
            {
                var go = _available.Pop();
                if (go != null) UnityEngine.Object.Destroy(go);
            }
            foreach (var go in _active.Values)
            {
                if (go != null) UnityEngine.Object.Destroy(go);
            }
            _active.Clear();
        }

    }
}
