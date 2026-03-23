using UnityEngine;
using CustomTiles;
using UnityEngine.Tilemaps;

namespace CustomTiles {
    public class WaterTile : CustomTileData
    {
        public WaterTile(TileBaseType tileType = TileBaseType.Water, string tileName = "WaterTile") : base(tileType, tileName)
        {
            
        }

        public override void OnSet(Vector2Int position, GridData gridData)
        {
            // 必要であれば水タイル設置時のロジックをここに実装
        }
    }
}
