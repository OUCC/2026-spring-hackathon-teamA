using System;
using System.Collections.Generic;
using UnityEngine;
using FloorBreaker.Shared.Domain.Primitives;

namespace FloorBreaker.Bombs.Presentation
{
    /// <summary>
    /// ボム着弾時の爆発 VFX パーティクルプール。
    /// 炎ボム / 滑落ボム で別プールを管理。TileFireVfxPool と同じパターン。
    /// </summary>
    public sealed class BombExplosionVfxPool : IDisposable
    {
        private static readonly Vector3 VfxOffset = new(0f, 0.1f, -0.5f);

        private readonly GameObject _firePrefab;
        private readonly GameObject _fallPrefab;
        private readonly Transform _poolParent;
        private readonly float _scale;
        private readonly float _duration;

        private readonly Stack<GameObject> _fireAvailable = new();
        private readonly Stack<GameObject> _fallAvailable = new();
        private readonly List<(GameObject go, float timer, BombType type)> _active = new();

        public BombExplosionVfxPool(
            GameObject firePrefab,
            GameObject fallPrefab,
            Transform poolParent,
            float scale,
            float duration,
            int initialCapacity = 4)
        {
            _firePrefab = firePrefab;
            _fallPrefab = fallPrefab;
            _poolParent = poolParent;
            _scale = scale;
            _duration = duration;

            PreWarm(_firePrefab, _fireAvailable, initialCapacity);
            PreWarm(_fallPrefab, _fallAvailable, initialCapacity);
        }

        private void PreWarm(GameObject prefab, Stack<GameObject> pool, int count)
        {
            if (prefab == null) return;
            for (int i = 0; i < count; i++)
            {
                var go = UnityEngine.Object.Instantiate(prefab, _poolParent);
                go.SetActive(false);
                pool.Push(go);
            }
        }

        public void Spawn(BombType type, Vector3 worldPos)
        {
            var prefab = type == BombType.Fire ? _firePrefab : _fallPrefab;
            if (prefab == null) return;

            var pool = type == BombType.Fire ? _fireAvailable : _fallAvailable;
            GameObject go;

            if (pool.Count > 0)
            {
                go = pool.Pop();
            }
            else
            {
                go = UnityEngine.Object.Instantiate(prefab, _poolParent);
            }

            go.transform.position = worldPos + VfxOffset;
            go.transform.localScale = new Vector3(_scale, _scale, _scale);
            go.SetActive(true);

            var ps = go.GetComponentInChildren<ParticleSystem>();
            if (ps != null)
            {
                ps.Clear();
                ps.Play();
            }

            _active.Add((go, _duration, type));
        }

        public void Tick(float deltaTime)
        {
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                var entry = _active[i];
                entry.timer -= deltaTime;
                if (entry.timer <= 0f)
                {
                    ReturnToPool(entry.go, entry.type);
                    _active.RemoveAt(i);
                }
                else
                {
                    _active[i] = entry;
                }
            }
        }

        private void ReturnToPool(GameObject go, BombType type)
        {
            var ps = go.GetComponentInChildren<ParticleSystem>();
            if (ps != null) ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            go.SetActive(false);
            var pool = type == BombType.Fire ? _fireAvailable : _fallAvailable;
            pool.Push(go);
        }

        public void Dispose()
        {
            foreach (var entry in _active)
            {
                if (entry.go != null) UnityEngine.Object.Destroy(entry.go);
            }
            _active.Clear();

            DestroyPool(_fireAvailable);
            DestroyPool(_fallAvailable);
        }

        private static void DestroyPool(Stack<GameObject> pool)
        {
            while (pool.Count > 0)
            {
                var go = pool.Pop();
                if (go != null) UnityEngine.Object.Destroy(go);
            }
        }
    }
}
