using System;
using Cysharp.Threading.Tasks;
using R3;
using FloorBreaker.Shared.Application.Interfaces;
using FloorBreaker.Shared.Domain.Primitives;
using FloorBreaker.Shared.Domain.Timing;
using FloorBreaker.MatchFlow.Application;

namespace FloorBreaker.UI.Result.Presentation
{
    /// <summary>
    /// リザルト画面を駆動する Presenter。
    /// </summary>
    public sealed class ResultPresenter : IDisposable
    {
        private readonly IDisposable _phaseSub;
        private readonly IDisposable _winnerSub;

        public ResultPresenter(
            ResultView view,
            MatchClock clock,
            MatchEndUseCase matchEnd,
            int playerCount,
            ISceneTransitionService sceneTransition)
        {
            _phaseSub = clock.CurrentPhase.Subscribe(phase =>
            {
                if (phase == GamePhase.Result)
                    view.Show();
                else
                    view.Hide();
            });

            _winnerSub = matchEnd.Winner.Subscribe(winner =>
            {
                if (!winner.HasValue) return;
                view.SetResult(winner, playerCount);
            });

            view.RematchButton.clicked += () => sceneTransition.LoadMatchAsync().Forget();
            view.TitleButton.clicked += () => sceneTransition.LoadTitleAsync().Forget();
            view.RematchButton2.clicked += () => sceneTransition.LoadMatchAsync().Forget();
            view.TitleButton2.clicked += () => sceneTransition.LoadTitleAsync().Forget();
        }

        public void Dispose()
        {
            _phaseSub.Dispose();
            _winnerSub.Dispose();
        }
    }
}
