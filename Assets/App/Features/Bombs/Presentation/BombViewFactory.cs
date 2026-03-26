using System.Collections.Generic;
using UnityEngine;
using FloorBreaker.Shared.Domain.Primitives;

namespace FloorBreaker.Bombs.Presentation
{
    /// <summary>
    /// BombFlightView の GameObject 生成 + プーリング。
    /// ボムは頻繁に生成/破棄されるため、プールで再利用する。
    /// </summary>
    public sealed class BombViewFactory : MonoBehaviour
    {
        [SerializeField] private BombSpriteConfig _config;

        public BombSpriteConfig Config => _config;

        private readonly Stack<BombFlightView> _pool = new();

        public BombFlightView GetView(PlayerId owner, BombType type, Vector3 spawnPos)
        {
            BombFlightView view;

            if (_pool.Count > 0)
            {
                view = _pool.Pop();
                view.Reinitialize(owner, type, _config);
            }
            else
            {
                view = CreateNewView(owner, type);
            }

            view.SetPositionImmediate(spawnPos);
            view.Show();
            return view;
        }

        public void ReturnView(BombFlightView view)
        {
            view.Hide();
            _pool.Push(view);
        }

        private BombFlightView CreateNewView(PlayerId owner, BombType type)
        {
            var go = new GameObject($"Bomb_{owner}_{type}");
            go.transform.SetParent(transform, false);
            var scale = _config.BombScale;
            go.transform.localScale = new Vector3(scale, scale, 1f);

            var renderer = go.AddComponent<SpriteRenderer>();
            var trail = go.AddComponent<TrailRenderer>();

            var view = go.AddComponent<BombFlightView>();
            view.Initialize(owner, type, renderer, trail, _config);

            return view;
        }
    }
}
