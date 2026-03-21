using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Tilemaps;
using CustomTiles;

public class TileMapUI : MonoBehaviour
{
    [SerializeField]
    private TileBase normaltile;

    [SerializeField]
    private TileBase firetile;

    [SerializeField]
    private List<TileTypeMap> tileTypeMapping;

    [SerializeField]
    private GridData gridData;
    private Tilemap tileMap;

    void Start()
    {
        tileMap = GetComponent<Tilemap>();
    }

    public void ChangeTileUI(Vector3Int position, CustomTiles.TileData newTile)
    {
        tileMap.SetTile(position, newTile.TileBase);
    }

    public void ChangeTilesUI(Dictionary<Vector2Int, TileBase> tilePositionPairs)
    {
        Vector3Int[] positions = tilePositionPairs.Keys.Select(ConvertVector.ToVector3Int).ToArray();
        TileBase[] tileBases = tilePositionPairs.Values.ToArray();

        tileMap.SetTiles(positions, tileBases);
    }

    public void NewTurn()
    {
        
    }

    public void OnEntityMoved(Vector2Int position)
    {
        
    }

    public void OnPlayerSteppedOnTile(Vector2Int position, Player player)
    {
        var tileData = gridData.GetTileData(position);
        tileData.OnPlayerSteppedOnTile(position, player);
    }

    public void ChangeTileUI(Vector3Int position)
    {
        tileMap.SetTile(position, gridData.GetTileData(ConvertVector.ToVector2Int(position)).TileBase);
    }

    void OnDestroy()
    {
        
    } 
}