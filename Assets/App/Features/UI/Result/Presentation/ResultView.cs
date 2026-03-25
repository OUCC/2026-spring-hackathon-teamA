using UnityEngine.UIElements;

namespace FloorBreaker.UI.Result.Presentation
{
    /// <summary>
    /// リザルト画面の VisualElement ラッパー。
    /// 左右独立ペイン構造。両ペインにボタンあり。
    /// </summary>
    public sealed class ResultView
    {
        private readonly VisualElement _resultRoot;
        private readonly Label _leftResultLabel;
        private readonly Label _rightResultLabel;

        public Button RematchButton { get; }
        public Button TitleButton { get; }
        public Button RematchButton2 { get; }
        public Button TitleButton2 { get; }

        public ResultView(VisualElement resultRoot)
        {
            _resultRoot = resultRoot;
            _leftResultLabel = resultRoot.Q<Label>("LeftResultLabel");
            _rightResultLabel = resultRoot.Q<Label>("RightResultLabel");
            RematchButton = resultRoot.Q<Button>("RematchButton");
            TitleButton = resultRoot.Q<Button>("TitleButton");
            RematchButton2 = resultRoot.Q<Button>("RematchButton2");
            TitleButton2 = resultRoot.Q<Button>("TitleButton2");
        }

        public void Show() => _resultRoot.RemoveFromClassList("result-root--hidden");
        public void Hide() => _resultRoot.AddToClassList("result-root--hidden");

        public void SetResult(bool p1Won)
        {
            _leftResultLabel.text = p1Won ? "WIN!" : "LOSE";
            _rightResultLabel.text = p1Won ? "LOSE" : "WIN!";
        }
    }
}
