using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using FloorBreaker.Shared.Application.Interfaces;
using FloorBreaker.Input.Infrastructure;
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
        [SerializeField] private InputActionAsset _inputActions;

        // ボタン
        public Button ModeButton2P { get; private set; }
        public Button ModeButton1P { get; private set; }
        public Button ModeButtonCPU { get; private set; }
        public Button QuitButton { get; private set; }
        public Button KeyConfigButton { get; private set; }
        public Button KeyConfigResetButton { get; private set; }
        public Button KeyConfigCloseButton { get; private set; }

        // 音量スライダー
        public Slider VolumeMaster { get; private set; }
        public Slider VolumeBgm { get; private set; }
        public Slider VolumeSfx { get; private set; }
        public Label VolumeMasterLabel { get; private set; }
        public Label VolumeBgmLabel { get; private set; }
        public Label VolumeSfxLabel { get; private set; }

        // キーコンフィグ
        public VisualElement KeyConfigOverlay { get; private set; }
        public VisualElement KeyConfigP1 { get; private set; }
        public VisualElement KeyConfigP2 { get; private set; }

        // 操作説明ラベル
        public Label P1Move { get; private set; }
        public Label P1AimLock { get; private set; }
        public Label P1FireBomb { get; private set; }
        public Label P1BreakBomb { get; private set; }
        public Label P2Move { get; private set; }
        public Label P2AimLock { get; private set; }
        public Label P2FireBomb { get; private set; }
        public Label P2BreakBomb { get; private set; }

        public InputActionAsset InputActions => _inputActions;

        private void Awake()
        {
            var root = GetComponent<UIDocument>().rootVisualElement;

            ModeButton2P = root.Q<Button>("ModeButton_2P");
            ModeButton1P = root.Q<Button>("ModeButton_1P");
            ModeButtonCPU = root.Q<Button>("ModeButton_CPU");
            QuitButton = root.Q<Button>("QuitButton");
            KeyConfigButton = root.Q<Button>("KeyConfigButton");
            KeyConfigResetButton = root.Q<Button>("KeyConfigResetButton");
            KeyConfigCloseButton = root.Q<Button>("KeyConfigCloseButton");

            // 音量
            VolumeMaster = root.Q<Slider>("VolumeSlider_Master");
            VolumeBgm = root.Q<Slider>("VolumeSlider_Bgm");
            VolumeSfx = root.Q<Slider>("VolumeSlider_Sfx");
            VolumeMasterLabel = root.Q<Label>("VolumeLabel_Master");
            VolumeBgmLabel = root.Q<Label>("VolumeLabel_Bgm");
            VolumeSfxLabel = root.Q<Label>("VolumeLabel_Sfx");

            // キーコンフィグ
            KeyConfigOverlay = root.Q<VisualElement>("KeyConfigOverlay");
            KeyConfigP1 = root.Q<VisualElement>("KeyConfigP1");
            KeyConfigP2 = root.Q<VisualElement>("KeyConfigP2");

            // 操作説明
            P1Move = root.Q<Label>("P1_Move");
            P1AimLock = root.Q<Label>("P1_AimLock");
            P1FireBomb = root.Q<Label>("P1_FireBomb");
            P1BreakBomb = root.Q<Label>("P1_BreakBomb");
            P2Move = root.Q<Label>("P2_Move");
            P2AimLock = root.Q<Label>("P2_AimLock");
            P2FireBomb = root.Q<Label>("P2_FireBomb");
            P2BreakBomb = root.Q<Label>("P2_BreakBomb");
        }

        private void Start()
        {
            // IAudioService を実装した MonoBehaviour を検索 (DontDestroyOnLoad 含む)
            IAudioService audio = null;
            foreach (var mb in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
            {
                if (mb is IAudioService svc) { audio = svc; break; }
            }

            // InputActionAsset 自動検索（SerializeField 未設定時のフォールバック）
            if (_inputActions == null)
            {
                var assets = Resources.FindObjectsOfTypeAll<InputActionAsset>();
                foreach (var asset in assets)
                {
                    if (asset.name == "FloorBreakerActions" || asset.FindActionMap("Gameplay_P1") != null)
                    {
                        _inputActions = asset;
                        break;
                    }
                }
            }

            // KeyRebindingService 生成 + 保存済みバインド適用
            KeyRebindingService rebindService = null;
            if (_inputActions != null)
            {
                rebindService = new KeyRebindingService(_inputActions);
                rebindService.LoadOverrides();
            }

            new TitlePresenter(this, audio, rebindService);
        }
    }
}
