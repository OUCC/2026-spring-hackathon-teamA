using UnityEngine;

public class ConvertVector
{
    public static Vector3Int ToVector3Int(Vector2Int vec2)
    {
        return new Vector3Int(vec2.x, vec2.y, 0);
    }

    public static Vector3Int ToVector3Int(Vector3 vec3)
    {
        return new Vector3Int(Vector3Int.RoundToInt(vec3).x, Vector3Int.RoundToInt(vec3).y, Vector3Int.RoundToInt(vec3).z);
    }

    public static Vector2Int ToVector2Int(Vector3Int vec3)
    {
        return new Vector2Int(vec3.x, vec3.y);
    }
}
