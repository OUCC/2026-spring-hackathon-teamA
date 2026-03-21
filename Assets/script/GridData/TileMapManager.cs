using System;
using System.Collections.Generic;
using System.Numerics;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Tilemaps;
using Tiles;

public class TileMapManager : MonoBehaviour
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

    public void ChangeTile(Vector3Int position, Tiles.TileData newTile)
    {
        gridData.ChangeTile(position, newTile);
    }

    public void NewTurn()
    {
        
    }

    public void OnEntityMoved(Vector3Int position)
    {
        
    }

    public void OnPlayerSteppedOnTile(Vector3Int position, Player player)
    {
        var tileData = gridData.GetTileData(position);
        tileData.OnPlayerSteppedOnTile(position, player);
    }

    public void ChangeTileUI(Vector3Int position)
    {
        tileMap.SetTile(position, gridData.GetTileData(position).TileBase);
    }

    void OnDestroy()
    {
        
    }   
}