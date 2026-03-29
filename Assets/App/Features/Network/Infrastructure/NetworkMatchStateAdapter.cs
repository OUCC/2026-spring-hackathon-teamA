using Fusion;
using FloorBreaker.Shared.Domain.Primitives;
using FloorBreaker.Shared.Domain.Timing;

namespace FloorBreaker.Network.Infrastructure
{
    /// <summary>
    /// マッチフェーズ・タイマー・ポーズ状態の同期。
    /// ホスト: MatchClock → [Networked]
    /// クライアント: [Networked] → MatchClock
    /// </summary>
    public class NetworkMatchStateAdapter : NetworkBehaviour
    {
        [Networked] public int Phase { get; set; }
        [Networked] public float Remaining { get; set; }
        [Networked] public NetworkBool IsPaused { get; set; }

        private MatchClock _clock;
        private ChangeDetector _changeDetector;

        public void Initialize(MatchClock clock)
        {
            _clock = clock;
        }

        public override void Spawned()
        {
            _changeDetector = GetChangeDetector(ChangeDetector.Source.SimulationState);
        }

        /// <summary>ホスト側: Domain → [Networked] に転写。FixedUpdateNetwork 後に呼ぶ。</summary>
        public void SyncFromDomain()
        {
            if (_clock == null) return;
            Phase = (int)_clock.CurrentPhaseValue;
            Remaining = _clock.RemainingValue;
            IsPaused = _clock.IsPausedValue;
        }

        /// <summary>クライアント側: [Networked] → Domain ミラーに反映。Render() で呼ぶ。</summary>
        public void SyncToLocal()
        {
            if (_clock == null || Object.HasStateAuthority) return;

            foreach (var change in _changeDetector.DetectChanges(this))
            {
                switch (change)
                {
                    case nameof(Phase):
                        var phase = (GamePhase)Phase;
                        if (_clock.CurrentPhaseValue != phase)
                            _clock.SetPhase(phase);
                        break;
                    case nameof(Remaining):
                        _clock.ResetTimer(Remaining);
                        break;
                    case nameof(IsPaused):
                        if ((bool)IsPaused && !_clock.IsPausedValue) _clock.Pause();
                        else if (!(bool)IsPaused && _clock.IsPausedValue) _clock.Resume();
                        break;
                }
            }
        }
    }
}
