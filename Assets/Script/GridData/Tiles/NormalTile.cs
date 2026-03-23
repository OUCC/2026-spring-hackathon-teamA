using UnityEngine;
using CustomTiles;
using UnityEngine.Tilemaps;

[CreateAssetMenu(fileName = "NormalTile", menuName = "Tiles/NormalTile")]
public class NormalTile : CustomTileData
{
    public NormalTile(TileBaseType tileType = TileBaseType.Normal, string tileName = "NormalTile") : base(tileType, tileName)
    {
        
    }
}
