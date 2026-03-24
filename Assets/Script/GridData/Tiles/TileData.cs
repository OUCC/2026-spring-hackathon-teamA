using UnityEngine;
using UnityEngine.Tilemaps;

namespace CustomTiles {
    [System.Serializable]
    public abstract class CustomTileData
    {   
        public readonly TileBase TileBase;

        public readonly string TileName;

        //DIでGridDataを渡す?
        // public GridData GridData;

        public CustomTileData(string tileName, TileBase tileBase)
        {
            TileBase = tileBase;
            TileName = tileName;
        }
        
        public virtual void OnSet(Vector2Int position)
        {
            
        }

        public virtual void OnPlayerSteppedOnTile(Vector2Int position, Player player)
        {
            
        }

        public virtual void OnNextTurn()
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