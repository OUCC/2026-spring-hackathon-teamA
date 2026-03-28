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
    /// リザルト画面を駆動する Presenter。Human プレイヤーのみ表示対応。
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
            ISceneTransitionService sceneTransition,
            MatchModeConfig modeConfig,
            int[] humanIndices = null)
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
                view.SetResult(winner, playerCount, humanIndices);
            });

            for (int i = 0; i < view.PaneCount; i++)
            {
                view.GetRematchButton(i).clicked += () => sceneTransition.LoadMatchAsync().Forget(e => UnityEngine.Debug.LogException(e));
                view.GetTitleButton(i).clicked += () => sceneTransition.LoadTitleAsync().Forget(e => UnityEngine.Debug.LogException(e));
                view.GetSetupButton(i).clicked += () =>
                {
                    modeConfig.StartInSetupMode = true;
                    sceneTransition.LoadTitleAsync().Forget(e => UnityEngine.Debug.LogException(e));
                };
            }
        }

        public void Dispose()
        {
            _phaseSub.Dispose();
            _winnerSub.Dispose();
        }
    }
}
