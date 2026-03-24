using UnityEngine;
using UnityEngine.Tilemaps;
using CustomTiles;

namespace CustomTiles {
    public class FireTile : CustomTileData
    {
        private int _damage = 1;
        private readonly GridData _gridData;
        private readonly TileGenerator _tileGenerator;

        private int _spreadDepth = 0;
        public FireTile(
            TileBase tileBase,
            string tileName = "FireTile",
            int damage = 1,
            int spreadDepth = 0,
            GridData gridData = null,
            TileGenerator tileGenerator = null
        ) : base(tileName, tileBase)
        {
            _damage = damage;
            _spreadDepth = spreadDepth;
                _gridData = gridData;
            _tileGenerator = tileGenerator;
        }

        public override void OnSet(Vector2Int position)
        {
            SpreadFire(position); // タイルが設置されたときに火を拡散
        }

        public override void OnPlayerStepped(Vector2Int position, Player player)
        {
            player.TakeDamage(_damage);
        }

        public override void OnEnemyStepped(Vector2Int position, Enemy enemy)
        {
            enemy.TakeDamage(_damage);
        }

        private void SpreadFire(Vector2Int position)
        {
            if (_spreadDepth >= 1) return; // 拡散深度が1以上の場合は拡散しない

            if (_gridData == null || _tileGenerator == null)
            {
                Debug.LogWarning("FireTile.SpreadFire: GridData または TileGenerator が未設定のため拡散をスキップします。");
                return;
            }

            Vector2Int[] directions = new Vector2Int[]
            {
                new Vector2Int(1, 0),   // 右
                new Vector2Int(-1, 0),  // 左
                new Vector2Int(0, 1),   // 上
                new Vector2Int(0, -1)   // 下
            };

            foreach (Vector2Int dir in directions)
            {
                if (_gridData.TilesChangeOnNextTurn.TryGetValue(position + dir, out CustomTileData newTileData))
                {
                    if (newTileData is not FireTile && newTileData is not WaterTile)
                    {
                        _gridData.TilesChangeOnNextTurn[position + dir] = _tileGenerator.FireTile(_damage, _spreadDepth + 1);
                    }
                } 
                else 
                {
                    if (_gridData.TryGetTileData(position + dir, out CustomTileData existingTileData) && existingTileData is not FireTile && existingTileData is not WaterTile)
                    {
                        _gridData.TilesChangeOnNextTurn[position + dir] = _tileGenerator.FireTile(_damage, _spreadDepth + 1);
                    }
                }
            }
        }
    }
}
