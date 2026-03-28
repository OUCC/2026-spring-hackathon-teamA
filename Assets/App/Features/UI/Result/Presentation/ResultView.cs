using FloorBreaker.Shared.Domain.Primitives;
using UnityEngine.UIElements;

namespace FloorBreaker.UI.Result.Presentation
{
    /// <summary>
    /// リザルト画面の VisualElement ラッパー。
    /// 左右独立ペイン構造。両ペインにボタンあり。
    /// For now: pane[0]=Left, pane[1]=Right. Max 2 visible panes from UXML.
    /// </summary>
    public sealed class ResultView
    {
        private readonly VisualElement _resultRoot;
        private readonly Label[] _resultLabels;

        public Button RematchButton { get; }
        public Button TitleButton { get; }
        public Button RematchButton2 { get; }
        public Button TitleButton2 { get; }

        public ResultView(VisualElement resultRoot)
        {
            _resultRoot = resultRoot;
            _resultLabels = new[]
            {
                resultRoot.Q<Label>("LeftResultLabel"),
                resultRoot.Q<Label>("RightResultLabel")
            };
            RematchButton = resultRoot.Q<Button>("RematchButton");
            TitleButton = resultRoot.Q<Button>("TitleButton");
            RematchButton2 = resultRoot.Q<Button>("RematchButton2");
            TitleButton2 = resultRoot.Q<Button>("TitleButton2");
        }

        public void Show()
        {
            _resultRoot.AddToClassList("result-root--entering");
            _resultRoot.RemoveFromClassList("result-root--hidden");
            _resultRoot.schedule.Execute(() =>
                _resultRoot.RemoveFromClassList("result-root--entering"));
        }

        public void Hide() => _resultRoot.AddToClassList("result-root--hidden");

        /// <summary>
        /// Set result display. Shows "WIN!" on the winner's pane, "LOSE" on others.
        /// </summary>
        public void SetResult(PlayerId? winner, int playerCount)
        {
            int visiblePanes = System.Math.Min(playerCount, _resultLabels.Length);
            for (int i = 0; i < visiblePanes; i++)
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

        /// <summary>Legacy overload for backward compatibility.</summary>
        public void SetResult(bool p1Won)
        {
            _resultLabels[0].text = p1Won ? "WIN!" : "LOSE";
            if (_resultLabels.Length > 1)
                _resultLabels[1].text = p1Won ? "LOSE" : "WIN!";
        }
    }
}
