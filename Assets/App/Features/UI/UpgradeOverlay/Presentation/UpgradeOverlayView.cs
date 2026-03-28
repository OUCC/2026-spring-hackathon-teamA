using System;
using UnityEngine.UIElements;

namespace FloorBreaker.UI.UpgradeOverlay.Presentation
{
    /// <summary>
    /// 強化オーバーレイの VisualElement ラッパー。
    /// 各プレイヤーペインが自己完結型。
    /// For now: pane[0]=Left, pane[1]=Right. Max 2 visible panes from UXML.
    /// </summary>
    public sealed class UpgradeOverlayView
    {
        private readonly VisualElement _overlayRoot;
        private readonly Label[] _countdowns;
        private readonly VisualElement[] _cards;
        private readonly Label[] _statuses;
        private readonly VisualElement[] _panes;
        private readonly VisualElement[] _actions;
        private readonly Button[] _rerollBtns;
        private readonly Button[] _skipBtns;

        /// <summary>Number of panes available in the UXML (currently 2).</summary>
        public int PaneCount => _panes.Length;

        // Backward-compatible properties
        public VisualElement LeftCards => _cards[0];
        public VisualElement RightCards => _cards.Length > 1 ? _cards[1] : null;
        public Button LeftRerollBtn => _rerollBtns[0];
        public Button RightRerollBtn => _rerollBtns.Length > 1 ? _rerollBtns[1] : null;
        public Button LeftSkipBtn => _skipBtns[0];
        public Button RightSkipBtn => _skipBtns.Length > 1 ? _skipBtns[1] : null;

        public UpgradeOverlayView(VisualElement overlayRoot)
        {
            _overlayRoot = overlayRoot;

            // Left = index 0, Right = index 1
            _panes = new[]
            {
                overlayRoot.Q("LeftUpgradePane"),
                overlayRoot.Q("RightUpgradePane")
            };
            _countdowns = new[]
            {
                overlayRoot.Q<Label>("LeftCountdown"),
                overlayRoot.Q<Label>("RightCountdown")
            };
            _cards = new[]
            {
                overlayRoot.Q("LeftCards"),
                overlayRoot.Q("RightCards")
            };
            _statuses = new[]
            {
                overlayRoot.Q<Label>("LeftStatus"),
                overlayRoot.Q<Label>("RightStatus")
            };
            _actions = new[]
            {
                overlayRoot.Q("LeftActions"),
                overlayRoot.Q("RightActions")
            };
            _rerollBtns = new[]
            {
                overlayRoot.Q<Button>("LeftRerollBtn"),
                overlayRoot.Q<Button>("RightRerollBtn")
            };
            _skipBtns = new[]
            {
                overlayRoot.Q<Button>("LeftSkipBtn"),
                overlayRoot.Q<Button>("RightSkipBtn")
            };
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
                cd.text = text;
        }

        /// <summary>カウントダウンをパルスさせる (3-2-1 演出)。</summary>
        public void PulseCountdown()
        {
            foreach (var cd in _countdowns)
            {
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

        // --- Legacy Left/Right methods (backward compat) ---

        public void SetLeftStatus(string text) => SetStatus(0, text);
        public void SetRightStatus(string text) => SetStatus(1, text);

        public void SetLeftDone(bool done) => SetDone(0, done);
        public void SetRightDone(bool done) => SetDone(1, done);

        public void SetLeftRerollHighlight(bool on) => SetRerollHighlight(0, on);
        public void SetRightRerollHighlight(bool on) => SetRerollHighlight(1, on);

        public void SetLeftDoneHighlight(bool on) => SetDoneHighlight(0, on);
        public void SetRightDoneHighlight(bool on) => SetDoneHighlight(1, on);
    }
}
