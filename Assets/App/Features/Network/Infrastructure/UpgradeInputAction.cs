namespace FloorBreaker.Network.Infrastructure
{
    /// <summary>
    /// 強化フェーズ中のプレイヤー入力アクション。
    /// NetworkInput 経由でホストに送信される。
    /// </summary>
    public enum UpgradeInputAction : byte
    {
        None = 0,
        SelectCard0,
        SelectCard1,
        SelectCard2,
        Reroll,
        Skip,
        NavigateLeft,
        NavigateRight,
        NavigateUp,
        NavigateDown,
    }
}
