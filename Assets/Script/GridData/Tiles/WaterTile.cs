using UnityEngine;
using CustomTiles;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "WaterTile", menuName = "Tiles/WaterTile")]
public class WaterTile: TileData
{
    public override void OnSet(Vector2Int position, GridData gridData)
    {
        // Dictionary<Vector2Int, TileData> tilesToChange = new Dictionary<Vector2Int, TileData>();

        // // タイルがセットされたときのロジックをここに実装
        // for (int x = -1; x <= 1; x++)
        // {
        //     for (int y = -1; y <= 1; y++)
        //     {
        //         if (Mathf.Abs(x) == Mathf.Abs(y))
        //         {
        //             continue; // 斜めはスキップ
        //         } 
        //         else
        //         {
        //             Vector2Int adjacentPos = new Vector2Int(position.x + x, position.y + y);
        //             if (gridData.GetTileData(adjacentPos) != null)
        //             {
        //                 tilesToChange[adjacentPos] = this; // 隣接するタイルの位置とデータを辞書に追加
        //             }
        //         }
        //     }
        // }

        // gridData.ChangeTiles(tilesToChange); // 隣接する炎のタイルを水のタイルに変える
    }
}
