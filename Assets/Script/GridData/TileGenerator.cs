using UnityEngine;
using CustomTiles;
using UnityEngine.Tilemaps;

namespace CustomTiles {
    public class TileGenerator : ScriptableObject
    {
        public TileDataDefault FireTileDefault;
        public TileDataDefault WaterTileDefault;

        public CustomTileData FireTile(int damage = 1, int spreadDepth = 0)
        {
            return new FireTile(FireTileDefault.tileBase, FireTileDefault.tileName, damage, spreadDepth);
        }

        public CustomTileData WaterTile()
        {
            return new WaterTile(WaterTileDefault.tileBase, WaterTileDefault.tileName);
        }
    }
}

public enum TileBaseType
{
    Normal,
    Fire,
    Water
}