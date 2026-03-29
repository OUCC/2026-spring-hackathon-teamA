namespace FloorBreaker.Stage.Domain
{
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
