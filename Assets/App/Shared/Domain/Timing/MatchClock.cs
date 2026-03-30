using System;
using R3;

namespace FloorBreaker.Shared.Domain.Timing
{
    public sealed class MatchClock : IDisposable
    {
        private readonly ReactiveProperty<float> _remaining;
        private readonly ReactiveProperty<GamePhase> _currentPhase;
        private readonly ReactiveProperty<bool> _isPaused;

        public MatchClock(float phaseDuration)
        {
            _remaining = new ReactiveProperty<float>(phaseDuration);
            _currentPhase = new ReactiveProperty<GamePhase>(GamePhase.Title);
            _isPaused = new ReactiveProperty<bool>(false);
            PhaseDuration = phaseDuration;
        }

        public float PhaseDuration { get; }

        public ReadOnlyReactiveProperty<GamePhase> CurrentPhase => _currentPhase;
        public ReadOnlyReactiveProperty<bool> IsPaused => _isPaused;

        public float RemainingValue => _remaining.Value;
        public GamePhase CurrentPhaseValue => _currentPhase.Value;
        public bool IsPausedValue => _isPaused.Value;

        public void Tick(float deltaTime)
        {
            if (_isPaused.Value) return;
            _remaining.Value = MathF.Max(0f, _remaining.Value - deltaTime);
        }

        public void Pause() => _isPaused.Value = true;
        public void Resume() => _isPaused.Value = false;
        public void SetPhase(GamePhase phase) => _currentPhase.Value = phase;
        public void ResetTimer(float duration) => _remaining.Value = duration;
        public void ResetTimer() => _remaining.Value = PhaseDuration;

        public void Dispose()
        {
            _remaining.Dispose();
            _currentPhase.Dispose();
            _isPaused.Dispose();
        }
    }
}
