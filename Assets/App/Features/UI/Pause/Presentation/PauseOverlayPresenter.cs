using System;
using R3;
using Cysharp.Threading.Tasks;
using FloorBreaker.Shared.Domain.Timing;
using FloorBreaker.Shared.Application.Interfaces;
using FloorBreaker.MatchFlow.Application;

namespace FloorBreaker.UI.Pause.Presentation
{
    public sealed class PauseOverlayPresenter : IDisposable
    {
        private readonly PauseOverlayView _view;
        private readonly MatchPhaseScheduler _scheduler;
        private readonly ISceneTransitionService _sceneTransition;
        private readonly IDisposable _subscription;

        public PauseOverlayPresenter(
            PauseOverlayView view,
            MatchClock clock,
            MatchPhaseScheduler scheduler,
            ISceneTransitionService sceneTransition)
        {
            _view = view;
            _scheduler = scheduler;
            _sceneTransition = sceneTransition;

            _subscription = clock.IsPaused.Subscribe(paused =>
            {
                if (paused && clock.CurrentPhaseValue == GamePhase.MatchRunning)
                    _view.Show();
                else
                    _view.Hide();
            });

            _view.ResumeBtn.clicked += OnResume;
            _view.TitleBtn.clicked += OnTitle;
        }

        private void OnResume()
        {
            _scheduler.TogglePause();
        }

        private void OnTitle()
        {
            _scheduler.TogglePause();
            _sceneTransition.LoadTitleAsync().Forget();
        }

        public void Dispose()
        {
            _subscription?.Dispose();
            _view.ResumeBtn.clicked -= OnResume;
            _view.TitleBtn.clicked -= OnTitle;
        }
    }
}
