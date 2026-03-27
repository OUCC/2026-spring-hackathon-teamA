using FloorBreaker.Shared.Domain.Timing;

namespace FloorBreaker.CpuPlayer.Application
{
    /// <summary>
    /// CpuPlayerBrain と CpuUpgradeSelector を統合し、
    /// 現在のフェーズに応じて適切な方を駆動するコーディネータ。
    /// MatchTickRunner から Tick される。
    /// </summary>
    public sealed class CpuPlayerService
    {
        private readonly CpuPlayerBrain _brain;
        private readonly CpuUpgradeSelector _upgradeSelector;
        private readonly MatchClock _clock;

        private bool _upgradePhaseStarted;

        public CpuPlayerService(
            CpuPlayerBrain brain,
            CpuUpgradeSelector upgradeSelector,
            MatchClock clock)
        {
            _brain = brain;
            _upgradeSelector = upgradeSelector;
            _clock = clock;
        }

        public void Tick(float deltaTime)
        {
            var phase = _clock.CurrentPhaseValue;

            switch (phase)
            {
                case GamePhase.MatchRunning:
                    _upgradePhaseStarted = false;
                    _brain.Tick(deltaTime);
                    break;

                case GamePhase.UpgradePhase:
                    if (!_upgradePhaseStarted)
                    {
                        _upgradePhaseStarted = true;
                        _upgradeSelector.Reset();
                    }
                    _upgradeSelector.Tick(deltaTime);
                    break;
            }
        }
    }
}
