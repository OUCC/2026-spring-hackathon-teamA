using UnityEngine.UIElements;

namespace FloorBreaker.UI.UpgradeOverlay.Presentation
{
    /// <summary>
    /// 強化オーバーレイの VisualElement ラッパー。
    /// N-player 対応: 動的生成されたペイン配列を受け取る。
    /// </summary>
    public sealed class UpgradeOverlayView
    {
        private readonly VisualElement _overlayRoot;
        private readonly VisualElement[] _panes;
        private readonly Label[] _countdowns;
        private readonly VisualElement[] _cards;
        private readonly Label[] _statuses;
        private readonly VisualElement[] _actions;
        private readonly Button[] _rerollBtns;
        private readonly Button[] _skipBtns;

        public int PaneCount => _panes.Length;

        public UpgradeOverlayView(VisualElement overlayRoot, VisualElement[] panes)
        {
            _overlayRoot = overlayRoot;
            _panes = panes;
            int n = panes.Length;
            _countdowns = new Label[n];
            _cards = new VisualElement[n];
            _statuses = new Label[n];
            _actions = new VisualElement[n];
            _rerollBtns = new Button[n];
            _skipBtns = new Button[n];

            for (int i = 0; i < n; i++)
            {
                _countdowns[i] = panes[i].Q<Label>("Countdown");
                _cards[i] = panes[i].Q("Cards");
                _statuses[i] = panes[i].Q<Label>("Status");
                _actions[i] = panes[i].Q("Actions");
                _rerollBtns[i] = panes[i].Q<Button>("RerollBtn");
                _skipBtns[i] = panes[i].Q<Button>("SkipBtn");
            }
        }

        public void Show()
        {
            _overlayRoot.AddToClassList("upgrade-overlay--entering");
            _overlayRoot.RemoveFromClassList("upgrade-overlay--hidden");
            _overlayRoot.schedule.Execute(() =>
                _overlayRoot.RemoveFromClassList("upgrade-overlay--entering"));
        }

        public void Hide()
        {
            _overlayRoot.AddToClassList("upgrade-overlay--entering");
            _overlayRoot.schedule.Execute(() =>
                _overlayRoot.AddToClassList("upgrade-overlay--hidden"))
                .StartingIn(300);
        }

        public void SetCountdown(int seconds)
        {
            string text = seconds.ToString();
            foreach (var cd in _countdowns)
                if (cd != null) cd.text = text;
        }

        public void PulseCountdown()
        {
            foreach (var cd in _countdowns)
            {
                if (cd == null) continue;
                cd.AddToClassList("upgrade-pane__countdown--pulse");
                cd.schedule.Execute(() =>
                    cd.RemoveFromClassList("upgrade-pane__countdown--pulse")).StartingIn(50);
            }
        }

        // --- Indexed accessors ---

        public VisualElement GetCards(int index) => _cards[index];
        public void SetStatus(int index, string text) => _statuses[index].text = text;
        public void SetDone(int index, bool done) => _panes[index].EnableInClassList("upgrade-pane--done", done);

        public void SetRerollHighlight(int index, bool on)
            => _rerollBtns[index]?.EnableInClassList("upgrade-pane__reroll-btn--selected", on);

        public void SetDoneHighlight(int index, bool on)
            => _skipBtns[index]?.EnableInClassList("upgrade-pane__done-btn--selected", on);

        public Button GetRerollBtn(int index) => _rerollBtns[index];
        public Button GetSkipBtn(int index) => _skipBtns[index];
    }
}
