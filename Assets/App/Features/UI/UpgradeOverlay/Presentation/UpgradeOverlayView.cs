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

        public VisualElement LeftCards => _leftCards;
        public VisualElement RightCards => _rightCards;
        public Button LeftRerollBtn { get; }
        public Button RightRerollBtn { get; }

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
            LeftRerollBtn = overlayRoot.Q<Button>("LeftRerollBtn");
            RightRerollBtn = overlayRoot.Q<Button>("RightRerollBtn");
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
    }
}
