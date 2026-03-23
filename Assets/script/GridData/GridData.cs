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

    private Dictionary<Vector2Int, CustomTileData> gridDataDict;
    
    public Dictionary<Vector2Int, CustomTileData> TilesChangeOnNextTurn { get; private set; } = new Dictionary<Vector2Int, CustomTileData>();

    void Start()
    {
        gridDataDict = GenerateGridData();
        printData();
        tileMapUI = tileMap.GetComponent<TileMapUI>();
    }

    /// <summary>
    /// Tilemap からタイルリストを生成する
    /// </summary>
    public Dictionary<Vector2Int, CustomTileData> GenerateGridData()
    {
        this.tileMap.CompressBounds();
        BoundsInt bounds = this.tileMap.cellBounds;

        gridDataDict = new Dictionary<Vector2Int, CustomTileData>();

        // y 行ごとにリストを作成
        for (int y = bounds.yMin; y < bounds.yMax; y++)
        {
            for (int x = bounds.xMin; x < bounds.xMax; x++)
            {
                Vector2Int cellPos = new Vector2Int(x, y);
                TileBase tile = tileMap.GetTile(ConvertVector.ToVector3Int(cellPos));
                
                if (tile == null)
                {
                    continue;
                }

                //アカンコード
                CustomTileData newTile;
                if (tile.name.Contains("Fire"))
                {
                    newTile = new FireTile();
                }
                else if (tile.name.Contains("Water"))
                {
                    newTile = new WaterTile();
                }
                else
                {
                    newTile = new NormalTile();
                }

                gridDataDict[cellPos] = newTile;
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
                if (gridDataDict.TryGetValue(cellPos2D, out CustomTiles.CustomTileData tileData))
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

    public void ChangeTile(Vector2Int position, CustomTileData newTile)
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
            tileMapUI.ChangeTileUI(ConvertVector.ToVector3Int(position), newTile);
        }
        else
        {
            Debug.LogError($"ChangeTile: 座標 ({position.x}, {position.y}) にタイルデータが見つかりません。");
        }
    }

    public void ChangeTiles(Dictionary<Vector2Int, CustomTileData> tilesToChange)
    {
        foreach (var kvp in tilesToChange)
        {
            Vector2Int position = kvp.Key;
            CustomTileData newTile = kvp.Value;
            if (gridDataDict.ContainsKey(position))
            {
                gridDataDict[position] = newTile;
                tileMapUI.ChangeTileUI(ConvertVector.ToVector3Int(position), newTile);
                newTile.OnSet(position, this);
            }
            else
            {
                Debug.LogError($"ChangeTiles: 座標 ({position.x}, {position.y}) にタイルデータが見つかりません。");
            }
        }
    }
    
    public CustomTileData GetTileData(Vector2Int position)
    {
        if (gridDataDict.TryGetValue(position, out CustomTileData tileData))
        {
            return tileData;
        }
        else
        {
            Debug.LogError($"GetTileData: tile not found at ({position.x}, {position.y})");
            return null;
        }
    }

    public bool HasTileData(Vector2Int position)
    {
        return gridDataDict.ContainsKey(position);
    }

    public void AddTileDataNextTurn(Vector2Int position, CustomTileData tileData)
    {
        if (TilesChangeOnNextTurn.ContainsKey(position))
        {
            TilesChangeOnNextTurn[position] = tileData; // すでに変更予定のタイルがある場合は上書き
        }
    }

    public bool TryGetTileData(Vector2Int position, out CustomTileData tileData)
    {
        return gridDataDict.TryGetValue(position, out tileData);
    }

    public void OnNextTurn()
    {
        // ターン開始時にタイルの変化を処理
        ChangeTilesOnNextTurn();
        foreach (var kvp in gridDataDict)
        {
            Vector2Int position = kvp.Key;
            CustomTileData tileData = kvp.Value;
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
}
