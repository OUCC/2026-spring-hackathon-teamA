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
    private Tilemap tileMap;

    void Start()
    {
        tileMap = GetComponent<Tilemap>();
    }

    public void ChangeTileUI(Vector3Int position, TileBase newTile)
    {
        tileMap.SetTile(position, newTile);
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

    void OnDestroy()
    {
        
    } 
}