using UnityEngine;
using UnityEngine.Tilemaps;

namespace CustomTiles {
    [System.Serializable]
    public abstract class CustomTileData
    {   
        public readonly TileBaseType TileType;

        public readonly string TileName;

        //DIでGridDataを渡す?
        // public GridData GridData;

        public CustomTileData(TileBaseType tileType, string tileName)
        {
            TileType = tileType;
            TileName = tileName;
        }

        public virtual void OnNextTurn(Vector2Int position, GridData gridData)
        {
            
        }
        
        public virtual void OnSet(Vector2Int position, GridData gridData)
        {
            
        }

        public virtual void OnPlayerSteppedOnTile(Vector2Int position, GridData gridData, Player player)
        {
            
        }

        public virtual void OnEnemySteppedOnTile(Vector2Int position, GridData gridData, Enemy enemy)
        {
            
        }

        //プレイヤーとエネミーintefaceで統一してほしい
        // public virtual void OnEntitySteppedOnTile(Vector2Int position, GridData gridData, Entity entity)
        // {
            
        // }
    }
}