using UnityEngine;


namespace Tiles {
    [CreateAssetMenu(fileName = "FireTile", menuName = "Tiles/FireTile")]
    public class FireTile: TileData
    {
        [SerializeField]
        private int damage = 1;

        public override void OnPlayerSteppedOnTile(Vector3Int position, Player player)
        {
            player.TakeDamage(damage);
        }

        public override void OnTurn(Vector3Int position)
        {
            
        }
    }
}
