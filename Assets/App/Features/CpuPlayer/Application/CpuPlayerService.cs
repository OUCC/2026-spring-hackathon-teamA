using System.Collections.Generic;
using FloorBreaker.Shared.Domain.Timing;

namespace FloorBreaker.CpuPlayer.Application
{
    /// <summary>
    /// 複数の CpuPlayerBrain と CpuUpgradeSelector を統合し、
    /// 現在のフェーズに応じて適切な方を駆動するコーディネータ。
    /// MatchTickRunner から Tick される。
    /// </summary>
    public sealed class CpuPlayerService
    {
        private readonly IReadOnlyList<CpuPlayerBrain> _brains;
        private readonly IReadOnlyList<CpuUpgradeSelector> _selectors;
        private readonly MatchClock _clock;

        private bool _upgradePhaseStarted;

        public CpuPlayerService(
            IReadOnlyList<CpuPlayerBrain> brains,
            IReadOnlyList<CpuUpgradeSelector> selectors,
            MatchClock clock)
        {
            _brains = brains;
            _selectors = selectors;
            _clock = clock;
        }

        public void Tick(float deltaTime)
        {
            var phase = _clock.CurrentPhaseValue;

            switch (phase)
            {
                case GamePhase.MatchRunning:
                    _upgradePhaseStarted = false;
                    foreach (var brain in _brains)
                        brain.Tick(deltaTime);
                    break;

                case GamePhase.UpgradePhase:
                    if (!_upgradePhaseStarted)
                    {
                        _upgradePhaseStarted = true;
                        foreach (var sel in _selectors)
                            sel.Reset();
                    }
                    foreach (var sel in _selectors)
                        sel.Tick(deltaTime);
                    break;
            }
        }
    }
}
