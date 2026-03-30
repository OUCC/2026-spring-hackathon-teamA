using UnityEngine.UIElements;

namespace FloorBreaker.UI.Countdown.Presentation
{
    public sealed class CountdownOverlayView
    {
        private readonly VisualElement _overlayRoot;
        private readonly Label[] _numberLabels;
        private readonly Label[] _moveLabels;
        private readonly Label[] _fireLabels;
        private readonly Label[] _breakLabels;

        public CountdownOverlayView(VisualElement overlayRoot, VisualElement[] panes)
        {
            _overlayRoot = overlayRoot;
            int count = panes.Length;
            _numberLabels = new Label[count];
            _moveLabels = new Label[count];
            _fireLabels = new Label[count];
            _breakLabels = new Label[count];

            for (int i = 0; i < count; i++)
            {
                _numberLabels[i] = panes[i].Q<Label>("CountdownNumber");
                _moveLabels[i] = panes[i].Q<Label>("CountdownMoveLabel");
                _fireLabels[i] = panes[i].Q<Label>("CountdownFireLabel");
                _breakLabels[i] = panes[i].Q<Label>("CountdownBreakLabel");
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

        public void SetKeyGuide(int paneIndex, string moveText, string fireText, string breakText)
        {
            if (paneIndex < 0 || paneIndex >= _moveLabels.Length) return;
            if (_moveLabels[paneIndex] != null) _moveLabels[paneIndex].text = moveText;
            if (_fireLabels[paneIndex] != null) _fireLabels[paneIndex].text = fireText;
            if (_breakLabels[paneIndex] != null) _breakLabels[paneIndex].text = breakText;
        }
    }
}
