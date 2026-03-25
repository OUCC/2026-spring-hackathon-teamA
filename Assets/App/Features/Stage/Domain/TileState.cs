namespace FloorBreaker.Stage.Domain
{
    public enum TileState : byte
    {
        Normal,
        OnFire,
        Collapsing,
        Collapsed,
        PermanentlyDestroyed,
        Wall,
    }
}
