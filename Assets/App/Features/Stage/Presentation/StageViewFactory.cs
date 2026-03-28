using System.Collections.Generic;
using UnityEngine;
using FloorBreaker.Shared.Domain.Grid;
using FloorBreaker.Shared.Presentation.Common;
using FloorBreaker.Stage.Domain;

namespace FloorBreaker.Stage.Presentation
{
    public sealed class StageViewFactory : MonoBehaviour
    {
        [SerializeField] private GameObject _tilePrefab;
        [SerializeField] private TileSpriteConfig _config;

        public TileSpriteConfig Config => _config;

        public Dictionary<GridPos, TileView> CreateTileViews(
            StageModel model,
            TileCoordRange bounds)
        {
            var views = new Dictionary<GridPos, TileView>(bounds.Width * bounds.Height);
            var stageRoot = new GameObject("StageRoot");
            stageRoot.transform.SetParent(transform, false);

            foreach (var pos in bounds.GetAllPositions())
            {
                var worldPos = pos.ToWorldCenter().ToVector3(0f);
                var go = Instantiate(_tilePrefab, worldPos, Quaternion.identity, stageRoot.transform);
                go.name = $"Tile_{pos.X}_{pos.Y}";

                var renderer = go.GetComponent<SpriteRenderer>();
                var tileView = go.GetComponent<TileView>();
                if (tileView == null) tileView = go.AddComponent<TileView>();

                tileView.Initialize(pos, renderer);

                var data = model.GetTileData(pos);
                tileView.ApplyState(data, _config);

                views[pos] = tileView;
            }

            return views;
        }
    }
}
