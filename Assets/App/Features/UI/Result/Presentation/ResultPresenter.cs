using System;
using R3;
using UnityEngine.SceneManagement;
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
            MatchEndUseCase matchEnd)
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
                view.SetResult(winner.Value == PlayerId.Player1);
            });

            view.RematchButton.clicked += () => SceneManager.LoadScene("Match");
            view.TitleButton.clicked += () => SceneManager.LoadScene("Title");
        }

        public void Dispose()
        {
            _phaseSub.Dispose();
            _winnerSub.Dispose();
        }
    }
}
