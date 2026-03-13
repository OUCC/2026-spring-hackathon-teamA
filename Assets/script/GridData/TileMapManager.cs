using System;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Tilemaps;
public class TileMapanager : MonoBehaviour
{
    [SerializeField]
    private TileBase normaltile;

    [SerializeField]
    private TileBase firetile;

    private GridData GridData;
    private Tilemap tilemap;

    void Start()
    {
        tilemap = GetComponent<Tilemap>();
        GridData = new GridData(tilemap);
    }

    public void ChangeTile(int x, int y, TileType type)
    {
        switch (type)
        {
            case TileType.Empty:
                tilemap.SetTile(new Vector3Int(x, y, 0), null);
                break;
            case TileType.Normal:
                tilemap.SetTile(new Vector3Int(x, y, 0), normaltile);
                break;
            case TileType.Fire:
                tilemap.SetTile(new Vector3Int(x, y, 0), firetile); 
                break;
        }
    }

}