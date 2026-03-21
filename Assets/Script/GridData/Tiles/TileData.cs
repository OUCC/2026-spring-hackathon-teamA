using UnityEngine;
using UnityEngine.Tilemaps;

namespace Tiles {
    public abstract class TileData : ScriptableObject
    {   
        [SerializeField]
        private TileBase _tileBase;
        
        public TileBase TileBase => _tileBase;

        public string TileName;

        public virtual void OnTurn(Vector3Int position)
        {
            
        }
        public virtual void OnSet(Vector3Int position, Player player)
        {
            
        }

        public virtual void OnPlayerSteppedOnTile(Vector3Int position, Player player)
        {
            
        }
    }
}