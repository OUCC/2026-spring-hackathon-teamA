using UnityEngine;
using UnityEngine.Tilemaps;
using CustomTiles;
using R3;
using VContainer;
using VContainer.Unity;

namespace CustomTiles {
    public class FireTile : CustomTileData
    {
        private int _damage = 1;

        [Inject]
        private GridData gridData;

        [Inject]
        TileGenerator tileGenerator;

        private int _spreadDepth = 0;
        public FireTile(TileBase tileBase, string tileName = "FireTile",  int damage = 1, int spreadDepth = 0): base(tileName, tileBase)
        {
            _damage = damage;
            _spreadDepth = spreadDepth;
        }

        public override void OnSet(Vector2Int position)
        {
            SpreadFire(position); // タイルが設置されたときに火を拡散
        }

        public override void OnPlayerSteppedOnTile(Vector2Int position, Player player)
        {
            player.TakeDamage(_damage);
        }

        public override void OnEnemySteppedOnTile(Vector2Int position, GridData gridData, Enemy enemy)
        {
            enemy.TakeDamage(_damage);
        }

        private void SpreadFire(Vector2Int position)
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
                    if (newTileData is not FireTile fireTile && newTileData is not WaterTile)
                    {
                        gridData.TilesChangeOnNextTurn[position + dir] = tileGenerator.FireTile(_damage, _spreadDepth + 1);
                    }
                } 
                else 
                {
                    if (gridData.TryGetTileData(position + dir, out CustomTileData existingTileData) && existingTileData is not FireTile && existingTileData is not WaterTile)
                    {
                        gridData.TilesChangeOnNextTurn[position + dir] = tileGenerator.FireTile(_damage, _spreadDepth + 1);
                    }
                }
            }
        }
    }
}
