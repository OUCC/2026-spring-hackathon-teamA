namespace FloorBreaker.MatchFlow.Application
{
    /// <summary>
    /// Match の構成情報。DI 経由で各サービスに渡す。
    /// </summary>
    public sealed class MatchConfig
    {
        public bool IsCpuPlayer { get; }

        public MatchConfig(bool isCpuPlayer)
        {
            IsCpuPlayer = isCpuPlayer;
        }
    }
}
