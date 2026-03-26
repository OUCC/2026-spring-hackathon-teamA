using UnityEngine;
using UnityEngine.UIElements;
using FloorBreaker.Shared.Application.Interfaces;
using FloorBreaker.UI.Title.Presentation;

namespace FloorBreaker.UI.RuntimeUI.Documents
{
    /// <summary>
    /// Title シーンに配置する UIDocument の参照ホルダー。
    /// Awake で要素キャッシュ、Start で TitlePresenter を生成。
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class TitleUIDocument : MonoBehaviour
    {
        public Button ModeButton2P { get; private set; }
        public Button ModeButton1P { get; private set; }
        public Button ModeButtonCPU { get; private set; }
        public Button QuitButton { get; private set; }

        private void Awake()
        {
            var root = GetComponent<UIDocument>().rootVisualElement;
            ModeButton2P = root.Q<Button>("ModeButton_2P");
            ModeButton1P = root.Q<Button>("ModeButton_1P");
            ModeButtonCPU = root.Q<Button>("ModeButton_CPU");
            QuitButton = root.Q<Button>("QuitButton");
        }

        private void Start()
        {
            // IAudioService を実装した MonoBehaviour を検索 (DontDestroyOnLoad 含む)
            IAudioService audio = null;
            foreach (var mb in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
            {
                if (mb is IAudioService svc) { audio = svc; break; }
            }
            new TitlePresenter(this, audio);
        }
    }
}
