using UnityEngine;
using UnityEngine.Tilemaps;
using CustomTiles;
using Unity.VisualScripting;

namespace CustomTiles {
    [CreateAssetMenu(fileName = "FireTile", menuName = "Tiles/FireTile")]

    [System.Serializable]
    public class FireTile : CustomTileData
    {
        private int _damage = 1;

        private int _spreadDepth = 0;
        public FireTile(TileBaseType tileType = TileBaseType.Fire, string tileName = "FireTile", int damage = 1, int spreadDepth = 0): base(tileType, tileName)
        {
            _damage = damage;
            _spreadDepth = spreadDepth;
        }

        public override void OnPlayerSteppedOnTile(Vector2Int position, GridData gridData, Player player)
        {
            player.TakeDamage(_damage);
        }

        public override void OnEnemySteppedOnTile(Vector2Int position, GridData gridData, Enemy enemy)
        {
            enemy.TakeDamage(_damage);
        }

        public override void OnNextTurn(Vector2Int position, GridData gridData)
        {
            if(_spreadDepth < 1) // _spreadDepthが1未満の場合にのみ拡散する
            {
                SpreadFire(position, gridData);
            }
        }

        private void SpreadFire(Vector2Int position, GridData gridData)
        {
            if (_spreadDepth >= 1) return; // 拡散深度が1以上の場合は拡散しない

            Vector2Int[] directions = new Vector2Int[]
            {
                new Vector2Int(1, 0),   // 右
                new Vector2Int(-1, 0),  // 左
                new Vector2Int(0, 1),   // 上
                new Vector2Int(0, -1)   // 下
            };

            foreach (Vector2Int dir in directions)
            {
                if (gridData.TilesChangeOnNextTurn.TryGetValue(position + dir, out CustomTileData newTileData))
                {
                    if (newTileData.TileType == TileBaseType.Normal)
                    {
                        gridData.TilesChangeOnNextTurn[position + dir] = new FireTile(TileBaseType.Fire, "FireTile", _damage, _spreadDepth + 1);
                    }
                } 
                else 
                {
                    if (gridData.TryGetTileData(position + dir, out CustomTileData existingTileData) && existingTileData.TileType == TileBaseType.Normal)
                    {
                        gridData.TilesChangeOnNextTurn[position + dir] = new FireTile(TileBaseType.Fire, "FireTile", _damage, _spreadDepth + 1);
                    }
                }
            }
        }
    }
}
