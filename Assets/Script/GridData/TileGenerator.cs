using UnityEngine;
using CustomTiles;
using UnityEngine.Tilemaps;
using VContainer;

namespace CustomTiles {
    public class TileGenerator : MonoBehaviour
    {
        [SerializeField]
        private GridData _gridData;

        public TileBase fireTileBase;
        public TileBase waterTileBase;

        public CustomTileData FireTile(int damage = 1, int spreadDepth = 0)
        {
            return new FireTile(fireTileBase, "FireTile", damage, spreadDepth, _gridData, this);
        }

        public CustomTileData WaterTile()
        {
            return new WaterTile(waterTileBase, "WaterTile");
        }
    }
}

public enum TileBaseType
{
    Normal,
    Fire,
    Water
}