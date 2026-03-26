using UnityEngine;
using FloorBreaker.Shared.Domain.Grid;
using FloorBreaker.Shared.Domain.Primitives;
using FloorBreaker.Shared.Presentation.Common;

namespace FloorBreaker.Player.Presentation
{
    /// <summary>
    /// PlayerView の GameObject を生成するファクトリ。
    /// </summary>
    public sealed class PlayerViewFactory : MonoBehaviour
    {
        [SerializeField] private PlayerSpriteConfig _config;

        public PlayerSpriteConfig Config => _config;

        public PlayerView CreatePlayerView(PlayerId id, GridPos spawnPos)
        {
            var worldPos = spawnPos.ToWorldCenter().ToVector3(-1f);
            var go = new GameObject($"Player_{id}");
            go.transform.SetParent(transform, false);
            go.transform.position = worldPos;

            var scale = _config.PlayerScale;
            go.transform.localScale = new Vector3(scale, scale, 1f);

            var renderer = go.AddComponent<SpriteRenderer>();
            var view = go.AddComponent<PlayerView>();
            view.Initialize(id, renderer, _config);

            return view;
        }
    }
}
