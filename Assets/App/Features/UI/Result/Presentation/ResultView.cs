using FloorBreaker.Shared.Domain.Primitives;
using UnityEngine.UIElements;

namespace FloorBreaker.UI.Result.Presentation
{
    /// <summary>
    /// リザルト画面の VisualElement ラッパー。
    /// N-player 対応: 動的生成されたペイン配列を受け取る。
    /// </summary>
    public sealed class ResultView
    {
        private readonly VisualElement _resultRoot;
        private readonly Label[] _resultLabels;
        private readonly Button[] _rematchButtons;
        private readonly Button[] _setupButtons;
        private readonly Button[] _titleButtons;

        public int PaneCount => _resultLabels.Length;

        public ResultView(VisualElement resultRoot, VisualElement[] panes)
        {
            _resultRoot = resultRoot;
            int n = panes.Length;
            _resultLabels = new Label[n];
            _rematchButtons = new Button[n];
            _setupButtons = new Button[n];
            _titleButtons = new Button[n];

            for (int i = 0; i < n; i++)
            {
                _resultLabels[i] = panes[i].Q<Label>("ResultLabel");
                _rematchButtons[i] = panes[i].Q<Button>("RematchButton");
                _setupButtons[i] = panes[i].Q<Button>("SetupButton");
                _titleButtons[i] = panes[i].Q<Button>("TitleButton");
            }
        }

        public Button GetRematchButton(int i) => _rematchButtons[i];
        public Button GetSetupButton(int i) => _setupButtons[i];
        public Button GetTitleButton(int i) => _titleButtons[i];

        public void Show()
        {
            _resultRoot.AddToClassList("result-root--entering");
            _resultRoot.RemoveFromClassList("result-root--hidden");
            _resultRoot.schedule.Execute(() =>
                _resultRoot.RemoveFromClassList("result-root--entering"));
        }

        public void Hide() => _resultRoot.AddToClassList("result-root--hidden");

        public void SetResult(PlayerId? winner, int playerCount)
        {
            for (int i = 0; i < _resultLabels.Length; i++)
            {
                if (!winner.HasValue)
                {
                    _resultLabels[i].text = "DRAW";
                }
                else
                {
                    _resultLabels[i].text = winner.Value.Index == i ? "WIN!" : "LOSE";
                }
            }
        }
    }
}
