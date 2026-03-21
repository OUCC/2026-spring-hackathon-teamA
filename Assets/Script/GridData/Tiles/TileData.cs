using UnityEngine;
using UnityEngine.Tilemaps;

namespace CustomTiles {
    /*
    新しいタイルを追加するには、このTileDataを継承したクラスを作成し、必要なロジックを実装してください。そして、UnityエディタでScriptableObjectとして保存することでタイルを作れます。
    */
    public abstract class TileData : ScriptableObject
    {   
        [SerializeField]
        private TileBase _tileBase;
        
        public TileBase TileBase => _tileBase;

        public string TileName;

        //Znjectを使ってGridDataを注入する？とりあえず普通にDIで渡す形にする
        public GridData GridData;

        public virtual void OnNextTurn(Vector2Int position)
        {
            
        }
        public virtual void OnSet(Vector2Int position)
        {
            
        }

        public virtual void OnPlayerSteppedOnTile(Vector2Int position, Player player)
        {
            
        }
    }
}