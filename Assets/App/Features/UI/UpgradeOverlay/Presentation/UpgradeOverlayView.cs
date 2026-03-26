using UnityEngine.UIElements;

namespace FloorBreaker.UI.UpgradeOverlay.Presentation
{
    /// <summary>
    /// 強化オーバーレイの VisualElement ラッパー。
    /// 各プレイヤーペインが自己完結型。
    /// </summary>
    public sealed class UpgradeOverlayView
    {
        private readonly VisualElement _overlayRoot;
        private readonly Label _leftCountdown;
        private readonly Label _rightCountdown;
        private readonly VisualElement _leftCards;
        private readonly VisualElement _rightCards;
        private readonly Label _leftStatus;
        private readonly Label _rightStatus;
        private readonly VisualElement _leftPane;
        private readonly VisualElement _rightPane;
        private readonly VisualElement _leftActions;
        private readonly VisualElement _rightActions;

        public VisualElement LeftCards => _leftCards;
        public VisualElement RightCards => _rightCards;
        public Button LeftRerollBtn { get; }
        public Button RightRerollBtn { get; }
        public Button LeftSkipBtn { get; }
        public Button RightSkipBtn { get; }

        public UpgradeOverlayView(VisualElement overlayRoot)
        {
            _overlayRoot = overlayRoot;
            _leftPane = overlayRoot.Q("LeftUpgradePane");
            _rightPane = overlayRoot.Q("RightUpgradePane");
            _leftCountdown = overlayRoot.Q<Label>("LeftCountdown");
            _rightCountdown = overlayRoot.Q<Label>("RightCountdown");
            _leftCards = overlayRoot.Q("LeftCards");
            _rightCards = overlayRoot.Q("RightCards");
            _leftStatus = overlayRoot.Q<Label>("LeftStatus");
            _rightStatus = overlayRoot.Q<Label>("RightStatus");
            _leftActions = overlayRoot.Q("LeftActions");
            _rightActions = overlayRoot.Q("RightActions");
            LeftRerollBtn = overlayRoot.Q<Button>("LeftRerollBtn");
            RightRerollBtn = overlayRoot.Q<Button>("RightRerollBtn");
            LeftSkipBtn = overlayRoot.Q<Button>("LeftSkipBtn");
            RightSkipBtn = overlayRoot.Q<Button>("RightSkipBtn");
        }

        public void Show() => _overlayRoot.RemoveFromClassList("upgrade-overlay--hidden");
        public void Hide() => _overlayRoot.AddToClassList("upgrade-overlay--hidden");

        public void SetCountdown(int seconds)
        {
            string text = seconds.ToString();
            _leftCountdown.text = text;
            _rightCountdown.text = text;
        }

        public void SetLeftStatus(string text) => _leftStatus.text = text;
        public void SetRightStatus(string text) => _rightStatus.text = text;

        public void SetLeftDone(bool done) => _leftPane.EnableInClassList("upgrade-pane--done", done);
        public void SetRightDone(bool done) => _rightPane.EnableInClassList("upgrade-pane--done", done);

        /// <summary>リロールボタンのハイライト。</summary>
        public void SetLeftRerollHighlight(bool on)
            => LeftRerollBtn?.EnableInClassList("upgrade-pane__reroll-btn--selected", on);
        public void SetRightRerollHighlight(bool on)
            => RightRerollBtn?.EnableInClassList("upgrade-pane__reroll-btn--selected", on);

        /// <summary>完了ボタンのハイライト。</summary>
        public void SetLeftDoneHighlight(bool on)
            => LeftSkipBtn?.EnableInClassList("upgrade-pane__done-btn--selected", on);
        public void SetRightDoneHighlight(bool on)
            => RightSkipBtn?.EnableInClassList("upgrade-pane__done-btn--selected", on);
    }
}
