namespace FloorBreaker.Stage.Domain
{
    /// <summary>
    /// ステージ生成に必要なパラメータ。StageConfig (ScriptableObject) から変換して使う。
    /// </summary>
    public sealed class StageGenerationParams
    {
        public float WallSeedPercent { get; set; }
        public float WallGrowthChance { get; set; }
        public float WallTargetPercent { get; set; }
        public int SpawnProtectionRadius { get; set; }
        public int GasVeinCount { get; set; }
        public int GasVeinMinLength { get; set; }
        public int GasVeinMaxLength { get; set; }
        public PresetTileEntry[] PresetTiles { get; set; }
    }
}
