namespace FloorBreaker.Stage.Domain
{
    public struct TileData
    {
        public TileType Type;
        public TileCondition Condition;
        public short WarpPairId;

        public static TileData Default => new()
        {
            Type = TileType.Normal,
            Condition = TileCondition.Intact,
            WarpPairId = -1,
        };

        public bool IsPassable => IsTypePassable(Type) && IsConditionPassable(Condition);

        public static bool IsTypePassable(TileType type)
            => type != TileType.Wall && type != TileType.Bedrock;

        public static bool IsConditionPassable(TileCondition cond)
            => cond == TileCondition.Intact
            || cond == TileCondition.OnFire
            || cond == TileCondition.EternalFire;

        public static bool IsImpassableType(TileType type)
            => type == TileType.Wall || type == TileType.Bedrock;

        public static bool IsHoleCondition(TileCondition cond)
            => cond == TileCondition.Collapsed || cond == TileCondition.PermanentlyDestroyed;

        public static bool IsBurning(TileCondition cond)
            => cond == TileCondition.OnFire || cond == TileCondition.EternalFire;
    }
}
