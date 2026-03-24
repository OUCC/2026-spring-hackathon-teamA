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
    private TileBaseMapping tileBaseMapping;

    private Tilemap tileMap;

    void Start()
    {
        tileMap = GetComponent<Tilemap>();
    }

        public void ChangeTileUI(Vector3Int position, CustomTileData newTileData)
        {
        tileMap.SetTile(position, newTileData.TileBase);
    }

    public void ChangeTilesUI(Dictionary<Vector2Int, CustomTileData> tileDataDict)
    {
        Vector3Int[] positions = tileDataDict.Keys.Select(ConvertVector.ToVector3Int).ToArray();
        TileBase[] tileBases = tileDataDict.Values.Select(newTileData => newTileData.TileBase).ToArray();

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