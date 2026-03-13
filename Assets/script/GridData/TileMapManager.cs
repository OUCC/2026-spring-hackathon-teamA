using System;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Tilemaps;
public class TileMapanager : MonoBehaviour
{
    private GridData GridData;

    void Start()
    {
        GridData = GridData.GenerateFromTilemap(this.GetComponent<Tilemap>());
        
    }
}