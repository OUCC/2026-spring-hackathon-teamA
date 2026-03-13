using System;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Tilemaps;

public class GridData
{
    public int Width { get; private set; }
    public int Height { get; private set; }

    [SerializeField]
    private Tilemap Tilemap;

    // [row(y)][col(x)] の2次元リスト
    public List<List<TileData>> Tiles { get; private set; }

    /// <summary>
    /// Tilemap からタイルリストを生成する
    /// </summary>
    public static GridData GenerateFromTilemap(Tilemap tilemap)
    {
        tilemap.CompressBounds();
        BoundsInt bounds = tilemap.cellBounds;

        var data = new GridData
        {
            Width = bounds.size.x,
            Height = bounds.size.y,
            Tiles = new List<List<TileData>>()
        };

        // y 行ごとにリストを作成
        for (int y = 0; y < bounds.size.y; y++)
        {
            var row = new List<TileData>();

            for (int x = 0; x < bounds.size.x; x++)
            {
                // Tilemap 上の実座標
                var cellPos = new Vector3Int(
                    bounds.xMin + x,
                    bounds.yMin + y,
                    0
                );

                TileBase tile = tilemap.GetTile(cellPos);
                TileType type = (tile != null) ? TileType.Normal : TileType.Empty;

                row.Add(new TileData(x, y, type));
            }

            data.Tiles.Add(row);
        }

        return data;
    }

    

    public void ChangeTileType(int x, int y, TileType newType)
    {
        if (x < 0 || x >= Width || y < 0 || y >= Height)
        {
            Debug.LogError($"ChangeTileType: 座標 ({x}, {y}) はグリッドの範囲外です。");
            return;
        }

        Tiles[y][x].type = newType;
        Tilemap.SetTile(toGridPosition(x, y), newType == TileType.Normal ? Tilemap.GetTile(toGridPosition(x, y)) : null);
    }
    
    public TileData GetTileData(int x, int y)
    {
        var ListPos = toListPosition(x, y);
        (int listX, int listY) = (ListPos.x, ListPos.y);

        if (listX < 0 || listX >= Width || listY < 0 || listY >= Height)
        {
            Debug.LogError($"GetTileData: 座標 ({x}, {y}) はグリッドの範囲外です。");
            return null;
        }

        return Tiles[listY][listX];
    }

    public Vector3Int toListPosition(int Gridx, int Gridy)
    {
        BoundsInt bounds = this.Tilemap.cellBounds;
        return new Vector3Int(Gridx - bounds.xMin, Gridy - bounds.yMin, 0);
    }

    public Vector3Int toGridPosition(int listX, int listY)
    {
        BoundsInt bounds = this.Tilemap.cellBounds;
        return new Vector3Int(listX + bounds.xMin, listY + bounds.yMin, 0);
    }
    

}
