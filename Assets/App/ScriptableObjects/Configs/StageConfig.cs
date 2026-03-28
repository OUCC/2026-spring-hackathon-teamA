using UnityEngine;
using FloorBreaker.Stage.Domain;

namespace FloorBreaker.ScriptableObjects.Configs
{
    [CreateAssetMenu(fileName = "StageConfig", menuName = "FloorBreaker/Stage Config")]
    public sealed class StageConfig : ScriptableObject
    {
        [Header("Meta")]
        [SerializeField] private string displayName = "Standard";
        [SerializeField] private string description = "";
        [SerializeField] private Sprite thumbnail;

        [Header("Grid")]
        [SerializeField] private int width = 30;
        [SerializeField] private int height = 30;

        [Header("Wall Generation")]
        [SerializeField] private float wallSeedPercent = 0.08f;
        [SerializeField] private float wallGrowthChance = 0.4f;
        [SerializeField] private float wallTargetPercent = 0.2f;
        [SerializeField] private int spawnProtectionRadius = 2;

        [Header("Gas Generation")]
        [SerializeField] private int gasVeinCount;
        [SerializeField] private int gasVeinMinLength = 3;
        [SerializeField] private int gasVeinMaxLength = 8;

        [Header("Preset Tiles")]
        [SerializeField] private PresetTileEntry[] presetTiles;

        public string DisplayName => displayName;
        public string Description => description;
        public Sprite Thumbnail => thumbnail;

        public int Width => width;
        public int Height => height;
        public float WallSeedPercent => wallSeedPercent;
        public float WallGrowthChance => wallGrowthChance;
        public float WallTargetPercent => wallTargetPercent;
        public int SpawnProtectionRadius => spawnProtectionRadius;
        public int GasVeinCount => gasVeinCount;
        public int GasVeinMinLength => gasVeinMinLength;
        public int GasVeinMaxLength => gasVeinMaxLength;
        public PresetTileEntry[] PresetTiles => presetTiles;
    }

    [System.Serializable]
    public struct PresetTileEntry
    {
        public int x;
        public int y;
        public TileType type;
        public TileCondition condition;
        public short warpPairId;
    }
}
