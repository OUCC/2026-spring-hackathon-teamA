using UnityEngine;

namespace CustomTiles {
    [CreateAssetMenu(fileName = "FireTile", menuName = "Tiles/FireTile")]
    public class FireTile: TileData
    {
        [SerializeField]
        private int damage = 1;

        public override void OnPlayerSteppedOnTile(Vector2Int position, Player player)
        {
            player.TakeDamage(damage);
        }

        public override void OnNextTurn(Vector2Int position)
        {

            // タイルがセットされたときのロジックをここに実装
            for (int x = -1; x <= 1; x++)
            {
                for (int y = -1; y <= 1; y++)
                {
                    if (Mathf.Abs(x) == Mathf.Abs(y))
                    {
                        continue; // 斜めはスキップ
                    } 

                    Vector2Int adjacentPos = new Vector2Int(position.x + x, position.y + y);
                    TileData currentTileData = GridData.GetTileData(adjacentPos);
                    if (currentTileData != null)
                    {
                        if (GridData.TilesChangeOnNextTurn.TryGetValue(adjacentPos, out TileData newTileData))
                        {
                            if(newTileData is WaterTile)
                            {
                                // 隣接するタイルが水の場合は炎を消す
                                GridData.TilesChangeOnNextTurn[adjacentPos] = this; // nullをセットしてタイルを消す
                            }
                        }
                        else if (!(currentTileData is WaterTile || currentTileData is FireTile))
                        {
                            GridData.TilesChangeOnNextTurn[adjacentPos] = this; // 隣接するタイルの位置とデータを辞書に追加
                        }
                    }
                }
            }
        }
    }
}
