using UnityEngine.UIElements;

namespace FloorBreaker.UI.Countdown.Presentation
{
    public sealed class CountdownOverlayView
    {
        private readonly VisualElement _overlayRoot;
        private readonly Label[] _numberLabels;
        private readonly Label[] _moveKeysLabels;
        private readonly Label[] _aimKeyLabels;
        private readonly Label[] _fireKeyLabels;
        private readonly Label[] _breakKeyLabels;

        public CountdownOverlayView(VisualElement overlayRoot, VisualElement[] panes)
        {
            _overlayRoot = overlayRoot;
            int count = panes.Length;
            _numberLabels = new Label[count];
            _moveKeysLabels = new Label[count];
            _aimKeyLabels = new Label[count];
            _fireKeyLabels = new Label[count];
            _breakKeyLabels = new Label[count];

            for (int i = 0; i < count; i++)
            {
                _numberLabels[i] = panes[i].Q<Label>("CountdownNumber");
                _moveKeysLabels[i] = panes[i].Q<Label>("CountdownMoveKeys");
                _aimKeyLabels[i] = panes[i].Q<Label>("CountdownAimKey");
                _fireKeyLabels[i] = panes[i].Q<Label>("CountdownFireKey");
                _breakKeyLabels[i] = panes[i].Q<Label>("CountdownBreakKey");
            }
        }

        public void Show()
        {
            _overlayRoot.RemoveFromClassList("countdown-overlay--hidden");
        }

        public void Hide()
        {
            _overlayRoot.AddToClassList("countdown-overlay--hidden");
        }

        public void SetNumber(string text)
        {
            foreach (var label in _numberLabels)
            {
                if (label != null) label.text = text;
            }
        }

        public void PulseNumber()
        {
            foreach (var label in _numberLabels)
            {
                if (label == null) continue;
                label.AddToClassList("countdown-number--pulse");
                label.schedule.Execute(() =>
                    label.RemoveFromClassList("countdown-number--pulse")).StartingIn(100);
            }
        }

        public void SetKeyGuide(int paneIndex, string moveKeys, string aimKey,
            string fireKey, string breakKey)
        {
            if (paneIndex < 0 || paneIndex >= _moveKeysLabels.Length) return;
            if (_moveKeysLabels[paneIndex] != null) _moveKeysLabels[paneIndex].text = moveKeys;
            if (_aimKeyLabels[paneIndex] != null) _aimKeyLabels[paneIndex].text = aimKey;
            if (_fireKeyLabels[paneIndex] != null) _fireKeyLabels[paneIndex].text = fireKey;
            if (_breakKeyLabels[paneIndex] != null) _breakKeyLabels[paneIndex].text = breakKey;
        }

        /// <summary>キーキャップのグロー ON/OFF を切り替える（パルス用）。</summary>
        public void SetKeycapGlow(bool on)
        {
            ToggleGlow(_moveKeysLabels, on);
            ToggleGlow(_aimKeyLabels, on);
            ToggleGlow(_fireKeyLabels, on);
            ToggleGlow(_breakKeyLabels, on);
        }

        private static void ToggleGlow(Label[] labels, bool on)
        {
            foreach (var l in labels)
            {
                if (l == null) continue;
                if (on) l.AddToClassList("countdown-keycap--glow");
                else l.RemoveFromClassList("countdown-keycap--glow");
            }
        }
    }
}
