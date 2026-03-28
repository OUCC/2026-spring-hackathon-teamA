using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;
using FloorBreaker.Shared.Application.Interfaces;
using FloorBreaker.Input.Infrastructure;
using FloorBreaker.MatchFlow.Application;
using FloorBreaker.UI.RuntimeUI.Documents;

namespace FloorBreaker.UI.Title.Presentation
{
    /// <summary>
    /// タイトル画面のボタンイベント、BGM、音量、キーコンフィグを管理する Presenter。
    /// TitleInitializer から生成される。
    /// </summary>
    public sealed class TitlePresenter
    {
        private readonly TitleUIDocument _doc;
        private readonly IAudioService _audio;
        private readonly KeyRebindingService _rebindService;
        private readonly KeyRebindingPresenter _rebindPresenter;

        public TitlePresenter(
            TitleUIDocument doc,
            IAudioService audio,
            KeyRebindingService rebindService,
            MatchModeConfig modeConfig,
            ISceneTransitionService sceneTransition)
        {
            _doc = doc;
            _audio = audio;
            _rebindService = rebindService;

            // BGM 再生
            audio?.PlayBgm(SfxIds.BgmTitle);

            // 2P 対戦
            doc.ModeButton2P?.RegisterCallback<ClickEvent>(_ =>
            {
                modeConfig.IsCpuSlot = new[] { false, false, false, false };
                modeConfig.PlayerCount = 2;
                audio?.StopBgm(0.5f);
                sceneTransition.LoadMatchAsync().Forget();
            });

            // vs CPU
            doc.ModeButton1P?.RegisterCallback<ClickEvent>(_ =>
            {
                modeConfig.IsCpuSlot = new[] { false, true, false, false };
                modeConfig.PlayerCount = 2;
                audio?.StopBgm(0.5f);
                sceneTransition.LoadMatchAsync().Forget();
            });

            // 観戦モード — 無効 (Coming Soon)
            doc.ModeButtonCPU?.SetEnabled(false);

            // 終了
            doc.QuitButton?.RegisterCallback<ClickEvent>(_ =>
            {
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
            });

            // 音量スライダー
            SetupVolumeSliders();

            // キーコンフィグ
            if (rebindService != null)
            {
                _rebindPresenter = new KeyRebindingPresenter(
                    rebindService,
                    doc.KeyConfigOverlay,
                    doc.KeyConfigP1,
                    doc.KeyConfigP2,
                    doc.KeyConfigResetButton,
                    doc.KeyConfigCloseButton,
                    UpdateControlsDisplay);

                doc.KeyConfigButton?.RegisterCallback<ClickEvent>(_ => _rebindPresenter.Show());

                // 操作説明テキストを現在のバインドで更新
                UpdateControlsDisplay();
            }
        }

        // ─── 音量 ──────────────────────────────────────────────

        private void SetupVolumeSliders()
        {
            if (_audio == null) return;

            // 初期値を AudioService から読み取り
            SetSlider(_doc.VolumeMaster, _doc.VolumeMasterLabel, _audio.MasterVolume);
            SetSlider(_doc.VolumeBgm, _doc.VolumeBgmLabel, _audio.BgmVolume);
            SetSlider(_doc.VolumeSfx, _doc.VolumeSfxLabel, _audio.SfxVolume);

            _doc.VolumeMaster?.RegisterValueChangedCallback(evt =>
            {
                _audio.SetMasterVolume(evt.newValue / 100f);
                UpdateVolumeLabel(_doc.VolumeMasterLabel, evt.newValue);
                _audio.PlaySfx(SfxIds.UiNavigate);
            });

            _doc.VolumeBgm?.RegisterValueChangedCallback(evt =>
            {
                _audio.SetBgmVolumeLevel(evt.newValue / 100f);
                UpdateVolumeLabel(_doc.VolumeBgmLabel, evt.newValue);
            });

            _doc.VolumeSfx?.RegisterValueChangedCallback(evt =>
            {
                _audio.SetSfxVolume(evt.newValue / 100f);
                UpdateVolumeLabel(_doc.VolumeSfxLabel, evt.newValue);
                _audio.PlaySfx(SfxIds.UiNavigate);
            });
        }

        private static void SetSlider(Slider slider, Label label, float normalizedValue)
        {
            if (slider == null) return;
            float displayValue = normalizedValue * 100f;
            slider.SetValueWithoutNotify(displayValue);
            UpdateVolumeLabel(label, displayValue);
        }

        private static void UpdateVolumeLabel(Label label, float value)
        {
            if (label != null) label.text = $"{Mathf.RoundToInt(value)}%";
        }

        // ─── 操作説明更新 ──────────────────────────────────────

        private void UpdateControlsDisplay()
        {
            if (_rebindService == null) return;

            UpdatePlayerControls("Gameplay_P1",
                _doc.P1Move, _doc.P1AimLock, _doc.P1FireBomb, _doc.P1BreakBomb);
            UpdatePlayerControls("Gameplay_P2",
                _doc.P2Move, _doc.P2AimLock, _doc.P2FireBomb, _doc.P2BreakBomb);
        }

        private void UpdatePlayerControls(string mapName,
            Label moveLabel, Label aimLabel, Label fireLabel, Label breakLabel)
        {
            var bindings = _rebindService.GetKeyboardBindings(mapName);

            // Move composite parts → "上 下 左 右" 形式
            string up = "", down = "", left = "", right = "";
            string aim = "", fire = "", breakBomb = "";

            foreach (var b in bindings)
            {
                if (b.Label.Contains("上")) up = b.DisplayString;
                else if (b.Label.Contains("下")) down = b.DisplayString;
                else if (b.Label.Contains("左")) left = b.DisplayString;
                else if (b.Label.Contains("右")) right = b.DisplayString;
                else if (b.Label == "照準") aim = b.DisplayString;
                else if (b.Label == "炎ボム") fire = b.DisplayString;
                else if (b.Label == "ブレークボム") breakBomb = b.DisplayString;
            }

            if (moveLabel != null)
                moveLabel.text = $"移動:  {up} {left} {down} {right}";
            if (aimLabel != null)
                aimLabel.text = $"照準:  {aim} (押しながら)";
            if (fireLabel != null)
                fireLabel.text = $"炎ボム:  {fire} (ホールド)";
            if (breakLabel != null)
                breakLabel.text = $"ブレークボム:  {breakBomb} (ホールド)";
        }
    }
}
