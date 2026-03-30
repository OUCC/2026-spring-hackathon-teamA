namespace FloorBreaker.Shared.Domain.Timing
{
    public enum GamePhase : byte
    {
        Title,
        Countdown,
        MatchRunning,
        StageShrink,
        UpgradePhase,
        Result,
    }
}
