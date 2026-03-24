using UnityEngine;
using UnityEngine.Tilemaps;
using CustomTiles;

[CreateAssetMenu(fileName = "TileDataDefault", menuName = "ScriptableObjects/TileDataDefault")]
public class TileDataDefault : ScriptableObject
{
    public string tileName;
    public TileBase tileBase;
}
