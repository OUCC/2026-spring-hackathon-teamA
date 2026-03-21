using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Tilemaps;
using CustomTiles;

public class GridData: MonoBehaviour
{
    [SerializeField]
    private Tilemap tileMap;

    private TileMapUI tileMapUI;
    // [row(y)][col(x)] の2次元リスト

    [SerializeField]
    private List<TileTypeMap> tileTypeMapping;
    private Dictionary<Vector2Int, CustomTiles.TileData> gridDataDict;
    public Dictionary<Vector2Int, CustomTiles.TileData> TilesChangeOnNextTurn { get; private set; } = new Dictionary<Vector2Int, CustomTiles.TileData>();

    void Start()
    {
        gridDataDict = GenerateGridData();
        printData();
        tileMapUI = tileMap.GetComponent<TileMapUI>();
    }

    /// <summary>
    /// Tilemap からタイルリストを生成する
    /// </summary>
    public Dictionary<Vector2Int, CustomTiles.TileData> GenerateGridData()
    {
        this.tileMap.CompressBounds();
        BoundsInt bounds = this.tileMap.cellBounds;

        gridDataDict = new Dictionary<Vector2Int, CustomTiles.TileData>();

        // y 行ごとにリストを作成
        for (int y = bounds.yMin; y < bounds.yMax; y++)
        {
            for (int x = bounds.xMin; x < bounds.xMax; x++)
            {
                Vector2Int cellPos = new Vector2Int(x, y);
                TileBase tile = tileMap.GetTile(ConvertVector.ToVector3Int(cellPos));
                CustomTiles.TileData newTileData = findTileDataByTileBase(tile, tileTypeMapping);
                //対応するタイルのデータオブジェクトが見つからなかった場合スキップ
                if (newTileData == null)
                {
                    continue;
                }
                gridDataDict[cellPos] = newTileData;
            }
        }
        return gridDataDict;
    }

    // デバッグ用：タイルの種類をコンソールに出力
    public void printData()
    {
        BoundsInt bounds = this.tileMap.cellBounds;
        for(int y = bounds.yMin; y < bounds.yMax; y++)
        {

            string rowStr = "";
            for(int x = bounds.xMin; x < bounds.xMax; x++)
            {
                var cellPos2D = new Vector2Int(x, y);
                if (gridDataDict.TryGetValue(cellPos2D, out CustomTiles.TileData tileData))
                {
                    rowStr += tileData.TileName + " ";
                }
                else
                {
                    rowStr += "* ";
                }
            }
            Debug.Log(rowStr);
        }
    }

    public void ChangeTile(Vector2Int position, CustomTiles.TileData newTile)
    {
        if (newTile == null)
        {
            Debug.LogError("ChangeTile: newTile が null です。Inspector で TileData を設定してください。");
            return;
        }
        
        if (gridDataDict.ContainsKey(position))
        {
            gridDataDict[position] = newTile;
            newTile.OnSet(position, this);
            tileMapUI.ChangeTileUI(ConvertVector.ToVector3Int(position), newTile.TileBase);
        }
        else
        {
            Debug.LogError($"ChangeTile: 座標 ({position.x}, {position.y}) にタイルデータが見つかりません。");
        }
    }

    public void ChangeTiles(Dictionary<Vector2Int, CustomTiles.TileData> tilesToChange)
    {
        foreach (var kvp in tilesToChange)
        {
            Vector2Int position = kvp.Key;
            CustomTiles.TileData newTile = kvp.Value;
            if (gridDataDict.ContainsKey(position))
            {
                gridDataDict[position] = newTile;
                tileMapUI.ChangeTileUI(ConvertVector.ToVector3Int(position), newTile.TileBase);
                newTile.OnSet(position, this);
            }
            else
            {
                Debug.LogError($"ChangeTiles: 座標 ({position.x}, {position.y}) にタイルデータが見つかりません。");
            }
        }
    }
    
    public CustomTiles.TileData GetTileData(Vector2Int position)
    {
        if (gridDataDict.TryGetValue(position, out CustomTiles.TileData tileData))
        {
            return tileData;
        }
        else
        {
            Debug.LogError($"GetTileData: tile not found at ({position.x}, {position.y})");
            return null;
        }
    }

    public CustomTiles.TileData TryGetTileData(Vector2Int position)
    {
        gridDataDict.TryGetValue(position, out CustomTiles.TileData tileData);
        return tileData;
    }

    public void OnNextTurn()
    {
        // ターン開始時にタイルの変化を処理
        ChangeTilesOnNextTurn();
        foreach (var kvp in gridDataDict)
        {
            Vector2Int position = kvp.Key;
            CustomTiles.TileData tileData = kvp.Value;
            tileData.OnNextTurn(position, this);
        }
    }

    public void OnPlayerSteppedOnTile(Vector2Int position, Player player)
    {
        var tileData = GetTileData(position);
        tileData.OnPlayerSteppedOnTile(position, this, player);
    }

    private void ChangeTilesOnNextTurn()
    {
        ChangeTiles(TilesChangeOnNextTurn);
        TilesChangeOnNextTurn.Clear(); // ターン開始時に辞書をクリアして次のターンに備える
    }

    // public void AddTileChangeOnNextTurn(Vector2Int position, CustomTiles.TileData tileData)
    // {
    //     TilesChangeOnNextTurn[position] = tileData;
    // }

    //タイルとタイルデータのスクリプタブルオブジェクトの対応の最善策が分からないので以下は応急処置用
    private CustomTiles.TileData findTileDataByTileBase(TileBase tileBase, List<TileTypeMap> tileTypeMapping)
    {
        foreach (var mapping in tileTypeMapping)
        {
            if (mapping.tileBase == tileBase)
            {
                return mapping.tileData;
            }
        }
        Debug.LogError($"TileMapManager: タイル {tileBase} に対応する TileDataBase が見つかりません。");
        return null;
    }
}

[System.Serializable]
public struct TileTypeMap
{
    public TileBase tileBase;
    public CustomTiles.TileData tileData;
}
