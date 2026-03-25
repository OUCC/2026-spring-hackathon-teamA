using UnityEngine;
using UnityEngine.Tilemaps;
using CustomTiles;
using System.Collections.Generic;
using UnityEngine.UIElements;

[CreateAssetMenu(fileName = "TileBaseMapping", menuName = "Scriptable Objects/TileBaseMapping")]
public class TileBaseMapping : ScriptableObject
{
    [System.Serializable]
    private struct MappingList
    {
        public TileBaseType tileType;
        public TileBase tileBase;
    }

    [SerializeField]
    private List<MappingList> mappingList;
    private Dictionary<TileBaseType, TileBase> tileBaseDictionary;

    private void GenerateDictionary()
    {
        tileBaseDictionary = new Dictionary<TileBaseType, TileBase>();
        foreach (MappingList mapping in mappingList)
        {
            tileBaseDictionary[mapping.tileType] = mapping.tileBase;
        }
        
    }

    public TileBase GetTileBase(TileBaseType tileType)
    {
        if (tileBaseDictionary == null)
        {
            GenerateDictionary();
        }
        
        if (tileBaseDictionary.TryGetValue(tileType, out TileBase tileBase))
        {
            return tileBase;
        }
        else
        {
            Debug.LogError($"TileBaseMapping: タイルタイプ {tileType} に対応する TileBase が見つかりません。");
            return null;
        }
    }
}
