namespace FloorBreaker.Stage.Domain
{
    public enum TileCondition : byte
    {
        Intact,
        OnFire,
        EternalFire,
        Collapsing,
        Collapsed,
        PermanentlyDestroyed,
    }
}
