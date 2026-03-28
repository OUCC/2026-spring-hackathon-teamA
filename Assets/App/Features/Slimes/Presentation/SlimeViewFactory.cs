using UnityEngine;
using FloorBreaker.Shared.Domain.Grid;
using FloorBreaker.Shared.Presentation.Common;
using FloorBreaker.Slimes.Domain;

namespace FloorBreaker.Slimes.Presentation
{
    /// <summary>
    /// SlimeView の GameObject を生成・破棄するファクトリ。
    /// </summary>
    public sealed class SlimeViewFactory : MonoBehaviour
    {
        [SerializeField] private SlimeSpriteConfig _config;

        public SlimeSpriteConfig Config => _config;

        public SlimeView CreateSlimeView(SlimeId id, SlimeType type, GridPos spawnPos)
        {
            var worldPos = spawnPos.ToWorldCenter().ToVector3(-1f);
            var go = new GameObject($"Slime_{id}");
            go.transform.SetParent(transform, false);
            go.transform.position = worldPos;

            var scale = _config.SlimeScale;
            go.transform.localScale = new Vector3(scale, scale, 1f);

            var renderer = go.AddComponent<SpriteRenderer>();
            if (_config.BaseMaterial != null)
                renderer.material = new Material(_config.BaseMaterial);
            var view = go.AddComponent<SlimeView>();
            view.Initialize(id, type, renderer, _config);

            return view;
        }

        public void DestroySlimeView(SlimeView view)
        {
            if (view != null)
            {
                Destroy(view.gameObject);
            }
        }
    }
}
