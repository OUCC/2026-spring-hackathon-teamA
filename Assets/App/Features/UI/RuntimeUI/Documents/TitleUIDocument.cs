using UnityEngine;
using UnityEngine.UIElements;

namespace FloorBreaker.UI.RuntimeUI.Documents
{
    /// <summary>
    /// Title シーンに配置する UIDocument の参照ホルダー。
    /// Awake で VisualElement をキャッシュし、TitleInitializer から参照される。
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class TitleUIDocument : MonoBehaviour
    {
        // --- 状態コンテナ ---
        public VisualElement TitleState { get; private set; }
        public VisualElement SetupState { get; private set; }
        public VisualElement SettingsOverlay { get; private set; }

        // --- TitleState ボタン ---
        public Button StartButton { get; private set; }
        public Button SettingsButton { get; private set; }
        public Button QuitButton { get; private set; }

        // --- SetupState: プレイヤースロット ---
        public VisualElement SlotP3 { get; private set; }
        public VisualElement SlotP4 { get; private set; }
        public VisualElement SlotContentP3 { get; private set; }
        public VisualElement SlotContentP4 { get; private set; }
        public Button SlotToggleP2 { get; private set; }
        public Button SlotToggleP3 { get; private set; }
        public Button SlotToggleP4 { get; private set; }
        public Button SlotAddP3 { get; private set; }
        public Button SlotAddP4 { get; private set; }
        public Button SlotRemoveP3 { get; private set; }
        public Button SlotRemoveP4 { get; private set; }

        // --- SetupState: ステージ選択 ---
        public VisualElement StageList { get; private set; }
        public VisualElement StagePreview { get; private set; }
        public VisualElement StagePreviewThumb { get; private set; }
        public Label StagePreviewName { get; private set; }
        public Label StagePreviewSize { get; private set; }
        public Label StagePreviewDesc { get; private set; }
        public VisualElement StagePreviewGimmicks { get; private set; }
        public VisualElement GimmickDetails { get; private set; }

        // --- SetupState: アクション ---
        public Button SetupStartButton { get; private set; }
        public Button SetupBackButton { get; private set; }

        // --- SettingsOverlay ---
        public Button SettingsCloseButton { get; private set; }

        // --- 音量スライダー ---
        public Slider VolumeMaster { get; private set; }
        public Slider VolumeBgm { get; private set; }
        public Slider VolumeSfx { get; private set; }
        public Label VolumeMasterLabel { get; private set; }
        public Label VolumeBgmLabel { get; private set; }
        public Label VolumeSfxLabel { get; private set; }

        // --- クレジット ---
        public VisualElement CreditsOverlay { get; private set; }
        public Button CreditsButton { get; private set; }
        public Button CreditsCloseButton { get; private set; }
        public Label CreditsText { get; private set; }

        // --- キーコンフィグ ---
        public Button KeyConfigButton { get; private set; }
        public Button KeyConfigResetButton { get; private set; }
        public Button KeyConfigCloseButton { get; private set; }
        public VisualElement KeyConfigOverlay { get; private set; }
        public VisualElement KeyConfigP1 { get; private set; }
        public VisualElement KeyConfigP2 { get; private set; }

        // --- 操作説明ラベル ---
        public Label P1Move { get; private set; }
        public Label P1AimLock { get; private set; }
        public Label P1FireBomb { get; private set; }
        public Label P1BreakBomb { get; private set; }
        public Label P2Move { get; private set; }
        public Label P2AimLock { get; private set; }
        public Label P2FireBomb { get; private set; }
        public Label P2BreakBomb { get; private set; }

        private void Awake()
        {
            var root = GetComponent<UIDocument>().rootVisualElement;

            // 状態コンテナ
            TitleState = root.Q("TitleState");
            SetupState = root.Q("SetupState");
            SettingsOverlay = root.Q("SettingsOverlay");

            // TitleState
            StartButton = root.Q<Button>("StartButton");
            SettingsButton = root.Q<Button>("SettingsButton");
            QuitButton = root.Q<Button>("QuitButton");

            // SetupState: スロット
            SlotP3 = root.Q("Slot_P3");
            SlotP4 = root.Q("Slot_P4");
            SlotContentP3 = root.Q("SlotContent_P3");
            SlotContentP4 = root.Q("SlotContent_P4");
            SlotToggleP2 = root.Q<Button>("SlotToggle_P2");
            SlotToggleP3 = root.Q<Button>("SlotToggle_P3");
            SlotToggleP4 = root.Q<Button>("SlotToggle_P4");
            SlotAddP3 = root.Q<Button>("SlotAdd_P3");
            SlotAddP4 = root.Q<Button>("SlotAdd_P4");
            SlotRemoveP3 = root.Q<Button>("SlotRemove_P3");
            SlotRemoveP4 = root.Q<Button>("SlotRemove_P4");

            // SetupState: ステージ
            StageList = root.Q("StageList");
            StagePreview = root.Q("StagePreview");
            StagePreviewThumb = root.Q("StagePreviewThumb");
            StagePreviewName = root.Q<Label>("StagePreviewName");
            StagePreviewSize = root.Q<Label>("StagePreviewSize");
            StagePreviewDesc = root.Q<Label>("StagePreviewDesc");
            StagePreviewGimmicks = root.Q("StagePreviewGimmicks");
            GimmickDetails = root.Q("GimmickDetails");

            // SetupState: アクション
            SetupStartButton = root.Q<Button>("SetupStartButton");
            SetupBackButton = root.Q<Button>("SetupBackButton");

            // SettingsOverlay
            SettingsCloseButton = root.Q<Button>("SettingsCloseButton");

            // 音量
            VolumeMaster = root.Q<Slider>("VolumeSlider_Master");
            VolumeBgm = root.Q<Slider>("VolumeSlider_Bgm");
            VolumeSfx = root.Q<Slider>("VolumeSlider_Sfx");
            VolumeMasterLabel = root.Q<Label>("VolumeLabel_Master");
            VolumeBgmLabel = root.Q<Label>("VolumeLabel_Bgm");
            VolumeSfxLabel = root.Q<Label>("VolumeLabel_Sfx");

            // クレジット
            CreditsOverlay = root.Q("CreditsOverlay");
            CreditsButton = root.Q<Button>("CreditsButton");
            CreditsCloseButton = root.Q<Button>("CreditsCloseButton");
            CreditsText = root.Q<Label>("CreditsText");

            // キーコンフィグ
            KeyConfigButton = root.Q<Button>("KeyConfigButton");
            KeyConfigResetButton = root.Q<Button>("KeyConfigResetButton");
            KeyConfigCloseButton = root.Q<Button>("KeyConfigCloseButton");
            KeyConfigOverlay = root.Q("KeyConfigOverlay");
            KeyConfigP1 = root.Q("KeyConfigP1");
            KeyConfigP2 = root.Q("KeyConfigP2");

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
    }
}
