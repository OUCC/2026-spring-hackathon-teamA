using UnityEngine;

public enum TileType
{
    Empty,
    Normal,
}

public class TileData
{
    public TileType type;
    public int gridX;
    public int gridY;

    public TileData(int x, int y, TileType type = TileType.Empty)
    {
        this.gridX = x;
        this.gridY = y;
        this.type = type;
    }
}
