using UnityEngine;
using UnityEngine.Tilemaps;
using CustomTiles;

[CreateAssetMenu(fileName = "TileDataDefault", menuName = "ScriptableObjects/TileDataDefault")]
public class TileDataDefault : ScriptableObject
{
    public readonly string tileName;
    public readonly TileBase tileBase;
}
