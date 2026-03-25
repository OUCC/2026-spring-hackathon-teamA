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
            return new FireTile(gridData: _gridData, tileBase: fireTileBase, tileName: "FireTile", damage: damage, spreadDepth: spreadDepth, tileGenerator: this);
        }

        public CustomTileData WaterTile()
        {
            return new WaterTile(tileBase: waterTileBase, tileName: "WaterTile");
        }
    }
}

public enum TileBaseType
{
    Normal,
    Fire,
    Water
}