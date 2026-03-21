using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Tilemaps;
using Tiles;

public class GridData: MonoBehaviour
{
    [SerializeField]
    private Tilemap tileMap;

    private TileMapManager tileMapManager;
    // [row(y)][col(x)] の2次元リスト

    [SerializeField]
    private List<TileTypeMap> tileTypeMapping;
    private Dictionary<Vector2Int, Tiles.TileData> gridData;
    

    void Start()
    {
        gridData = GenerateGridData();
        printData();
        tileMapManager = tileMap.GetComponent<TileMapManager>();
    }

    /// <summary>
    /// Tilemap からタイルリストを生成する
    /// </summary>
    public Dictionary<Vector2Int, Tiles.TileData> GenerateGridData()
    {
        this.tileMap.CompressBounds();
        BoundsInt bounds = this.tileMap.cellBounds;

        gridData = new Dictionary<Vector2Int, Tiles.TileData>();

        // y 行ごとにリストを作成
        for (int y = bounds.yMin; y < bounds.yMax; y++)
        {
            for (int x = bounds.xMin; x < bounds.xMax; x++)
            {
                var cellPos2D = new Vector2Int(x, y);
                TileBase tile = tileMap.GetTile(toVector3Int(cellPos2D));
                //対応するタイルのデータオブジェクトが見つからなかった場合スキップ
                if (tile == null)
                {
                    continue;
                }
                gridData.Add(cellPos2D, findTileDataByTileBase(tile, tileTypeMapping));
            }
        }
        return gridData;
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
                if (gridData.TryGetValue(cellPos2D, out Tiles.TileData tileData))
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

    public void ChangeTile(Vector3Int position, Tiles.TileData newTile)
    {
        Vector2Int cellPos = toVector2Int(position);
        if (gridData.ContainsKey(cellPos))
        {
            gridData[cellPos] = newTile;
            tileMapManager.ChangeTileUI(position);
        }
        else
        {
            Debug.LogError($"ChangeTile: 座標 ({cellPos.x}, {cellPos.y}) にタイルデータが見つかりません。");
        }
    }
    
    public Tiles.TileData GetTileData(Vector3Int position)
    {
        Vector2Int cellPos = toVector2Int(position);
        if (gridData.TryGetValue(cellPos, out Tiles.TileData tileData))
        {
            return tileData;
        }
        else
        {
            Debug.LogError($"GetTileData: 座標 ({cellPos.x}, {cellPos.y}) にタイルデータが見つかりません。");
            return null;
        }
    }
    
    //タイルとタイルデータのスクリプタブルオブジェクトの対応の最善策が分からないので以下は応急処置用
    private Tiles.TileData findTileDataByTileBase(TileBase tileBase, List<TileTypeMap> tileTypeMapping)
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

    private Vector2Int toVector2Int(Vector3Int vec3)
    {
        return new Vector2Int(vec3.x, vec3.y);
    }

    private Vector3Int toVector3Int(Vector2Int vec2)
    {
        return new Vector3Int(vec2.x, vec2.y, 0);
    }
}

[System.Serializable]
public struct TileTypeMap
{
    public TileBase tileBase;
    public Tiles.TileData tileData;
}
