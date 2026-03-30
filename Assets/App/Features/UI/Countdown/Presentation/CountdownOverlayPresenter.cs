using System;
using System.Collections.Generic;
using R3;
using FloorBreaker.Shared.Domain.Timing;
using FloorBreaker.Shared.Application.Interfaces;
using FloorBreaker.MatchFlow.Application;

namespace FloorBreaker.UI.Countdown.Presentation
{
    public sealed class CountdownOverlayPresenter : IDisposable
    {
        private readonly CountdownOverlayView _view;
        private readonly MatchPhaseScheduler _scheduler;
        private readonly IAudioService _audio;
        private readonly List<IDisposable> _subscriptions = new();
        private int _lastDisplayedSecond = -1;

        public CountdownOverlayPresenter(
            CountdownOverlayView view,
            MatchClock clock,
            MatchPhaseScheduler scheduler,
            IAudioService audio)
        {
            _view = view;
            _scheduler = scheduler;
            _audio = audio;

            _subscriptions.Add(clock.CurrentPhase.Subscribe(phase =>
            {
                if (phase == GamePhase.Countdown)
                {
                    _lastDisplayedSecond = -1;
                    _view.Show();
                }
                else
                {
                    _view.Hide();
                }
            }));
        }

        public void UpdateCountdown()
        {
            if (_scheduler.State != SchedulerState.Countdown) return;

            float remaining = _scheduler.CountdownRemaining;
            int second = (int)MathF.Ceiling(remaining);

            if (second <= 0)
            {
                if (_lastDisplayedSecond != 0)
                {
                    _view.SetNumber("GO!");
                    _view.PulseNumber();
                    _lastDisplayedSecond = 0;
                }
                return;
            }

            if (second != _lastDisplayedSecond)
            {
                _lastDisplayedSecond = second;
                _view.SetNumber(second.ToString());
                _view.PulseNumber();
                _audio?.PlaySfx(SfxIds.CountdownTick);
            }
        }

        public void Dispose()
        {
            foreach (var sub in _subscriptions) sub.Dispose();
            _subscriptions.Clear();
        }
    }
}
