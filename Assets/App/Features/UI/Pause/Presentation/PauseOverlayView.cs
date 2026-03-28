using UnityEngine.UIElements;

namespace FloorBreaker.UI.Pause.Presentation
{
    public sealed class PauseOverlayView
    {
        private readonly VisualElement _root;

        public Button ResumeBtn { get; }
        public Button SettingsBtn { get; }
        public Button TitleBtn { get; }

        public PauseOverlayView(VisualElement root)
        {
            _root = root;
            ResumeBtn = root.Q<Button>("PauseResumeBtn");
            SettingsBtn = root.Q<Button>("PauseSettingsBtn");
            TitleBtn = root.Q<Button>("PauseTitleBtn");
        }

        public void Show()
        {
            _root.RemoveFromClassList("pause-overlay--hidden");
        }

        public void Hide()
        {
            _root.AddToClassList("pause-overlay--hidden");
        }
    }
}
