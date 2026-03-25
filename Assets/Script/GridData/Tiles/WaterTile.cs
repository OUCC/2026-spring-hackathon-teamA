using UnityEngine;
using CustomTiles;
using UnityEngine.Tilemaps;

namespace CustomTiles {
    public class WaterTile : CustomTileData
    {
        public WaterTile(TileBase tileBase, string tileName = "WaterTile") : base(tileName, tileBase)
        {
            
        }

        public override void OnSet(Vector2Int position)
        {
            // 必要であれば水タイル設置時のロジックをここに実装
        }
    }
}
