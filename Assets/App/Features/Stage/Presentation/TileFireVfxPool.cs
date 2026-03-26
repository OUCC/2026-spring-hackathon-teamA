using System;
using System.Collections.Generic;
using UnityEngine;
using FloorBreaker.Shared.Domain.Grid;


namespace FloorBreaker.Stage.Presentation
{
    public sealed class TileFireVfxPool : IDisposable
    {
        private static readonly Vector3 VfxOffset = new(0f, 0.1f, -0.1f);
        private static readonly Vector3 VfxScale = new(0.24f, 0.24f, 0.24f);

        private readonly GameObject _prefab;
        private readonly Transform _poolParent;
        private readonly Stack<GameObject> _available = new();
        private readonly Dictionary<GridPos, GameObject> _active = new();

        public TileFireVfxPool(GameObject prefab, Transform poolParent, int initialCapacity = 20)
        {
            _prefab = prefab;
            _poolParent = poolParent;

            if (_prefab == null) return;

            for (int i = 0; i < initialCapacity; i++)
            {
                var go = UnityEngine.Object.Instantiate(_prefab, _poolParent);
                go.SetActive(false);
                _available.Push(go);
            }
        }

        public void SpawnAt(GridPos pos, Vector3 worldPos)
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
            }

            go.transform.position = worldPos + VfxOffset;
            go.transform.localScale = VfxScale;
            go.SetActive(true);

            // パーティクルリセット
            var ps = go.GetComponentInChildren<ParticleSystem>();
            if (ps != null)
            {
                ps.Clear();
                ps.Play();
            }

            _active[pos] = go;
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
