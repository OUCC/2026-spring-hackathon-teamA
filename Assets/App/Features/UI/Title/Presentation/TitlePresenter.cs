using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;
using FloorBreaker.Shared.Application.Interfaces;
using FloorBreaker.Input.Infrastructure;
using FloorBreaker.MatchFlow.Application;
using FloorBreaker.Stage.Presentation;
using FloorBreaker.UI.RuntimeUI.Documents;
using DeviceType = FloorBreaker.Shared.Application.Interfaces.DeviceType;

namespace FloorBreaker.UI.Title.Presentation
{
    /// <summary>
    /// タイトル画面の Presenter。
    /// TitleState / SetupState / SettingsOverlay の3状態を管理する。
    /// </summary>
    public sealed class TitlePresenter
    {
        private readonly TitleUIDocument _doc;
        private readonly IAudioService _audio;
        private readonly MatchModeConfig _modeConfig;
        private readonly KeyRebindingService _rebindService;
        private readonly KeyRebindingPresenter _rebindPresenter;
        private readonly DeviceDetectionService _deviceDetection;
        private readonly NetworkLobbyPresenter _lobbyPresenter;
        private readonly StageSelectUI _stageSelectUI;

        public TitlePresenter(
            TitleUIDocument doc,
            IAudioService audio,
            KeyRebindingService rebindService,
            MatchModeConfig modeConfig,
            ISceneTransitionService sceneTransition,
            DeviceDetectionService deviceDetection = null,
            NetworkLobbyPresenter lobbyPresenter = null,
            TileSpriteConfig tileSpriteConfig = null,
            StagePreviewRenderer previewRenderer = null,
            IRandomProvider random = null)
        {
            _doc = doc;
            _audio = audio;
            _modeConfig = modeConfig;
            _rebindService = rebindService;
            _deviceDetection = deviceDetection ?? new DeviceDetectionService();
            _deviceDetection.OnDeviceAssigned += OnDeviceAssigned;
            _lobbyPresenter = lobbyPresenter;

            _stageSelectUI = new StageSelectUI(
                doc.StageList, doc.StagePreviewThumb, doc.StagePreviewName,
                doc.StagePreviewSize, doc.StagePreviewDesc,
                doc.StagePreviewGimmicks, doc.GimmickDetails,
                modeConfig, tileSpriteConfig,
                previewRenderer: previewRenderer,
                random: random,
                onStageSelected: _ => audio?.PlaySfx(SfxIds.UiNavigate));

            // BGM 再生
            audio?.PlayBgm(SfxIds.BgmTitle);

            // ── TitleState ボタン ──
            doc.StartButton?.RegisterCallback<ClickEvent>(_ =>
            {
                audio?.PlaySfx(SfxIds.UiNavigate);
                ShowSetupState();
            });
            doc.SettingsButton?.RegisterCallback<ClickEvent>(_ =>
            {
                audio?.PlaySfx(SfxIds.UiNavigate);
                ShowSettingsOverlay();
            });
            doc.QuitButton?.RegisterCallback<ClickEvent>(_ =>
            {
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
            });

            // ── SetupState ──
            SetupSlots();
            _stageSelectUI.PopulateStageList();

            doc.SetupStartButton?.RegisterCallback<ClickEvent>(_ =>
            {
                audio?.StopBgm(0.5f);
                sceneTransition.LoadMatchAsync().Forget(e => Debug.LogException(e));
            });
            doc.SetupBackButton?.RegisterCallback<ClickEvent>(_ =>
            {
                audio?.PlaySfx(SfxIds.UiNavigate);
                ShowTitleState();
            });

            // ── SettingsOverlay ──
            doc.SettingsCloseButton?.RegisterCallback<ClickEvent>(_ =>
            {
                audio?.PlaySfx(SfxIds.UiNavigate);
                HideSettingsOverlay();
            });
            SetupVolumeSliders();

            // ── KeyConfig ──
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
                UpdateControlsDisplay();
            }

            // ── Credits ──
            doc.CreditsButton?.RegisterCallback<ClickEvent>(_ =>
            {
                audio?.PlaySfx(SfxIds.UiNavigate);
                ShowCreditsOverlay();
            });
            doc.CreditsCloseButton?.RegisterCallback<ClickEvent>(_ =>
            {
                audio?.PlaySfx(SfxIds.UiNavigate);
                HideCreditsOverlay();
            });

            // ── Online ──
            doc.OnlineButton?.RegisterCallback<ClickEvent>(_ =>
            {
                audio?.PlaySfx(SfxIds.UiNavigate);
                ShowOnlineMenuState();
            });
            doc.CreateRoomButton?.RegisterCallback<ClickEvent>(_ =>
            {
                audio?.PlaySfx(SfxIds.UiNavigate);
                _lobbyPresenter?.EnterAsHost();
                ShowLobbyState();
            });
            doc.JoinRoomButton?.RegisterCallback<ClickEvent>(_ =>
            {
                audio?.PlaySfx(SfxIds.UiNavigate);
                _lobbyPresenter?.EnterAsClient();
                ShowLobbyState();
            });
            doc.OnlineBackButton?.RegisterCallback<ClickEvent>(_ =>
            {
                audio?.PlaySfx(SfxIds.UiNavigate);
                ShowTitleState();
            });
            doc.LobbyJoinButton?.RegisterCallback<ClickEvent>(_ =>
            {
                audio?.PlaySfx(SfxIds.UiNavigate);
                _lobbyPresenter?.JoinWithCode();
            });
            doc.LobbyStartButton?.RegisterCallback<ClickEvent>(_ =>
            {
                audio?.StopBgm(0.5f);
                _lobbyPresenter?.StartMatch();
            });
            doc.LobbyBackButton?.RegisterCallback<ClickEvent>(_ =>
            {
                audio?.PlaySfx(SfxIds.UiNavigate);
                _lobbyPresenter?.LeaveAsync().Forget();
                ShowOnlineMenuState();
            });

            // ── StartInSetupMode (リザルト「設定に戻る」から遷移) ──
            if (modeConfig.StartInSetupMode)
            {
                modeConfig.StartInSetupMode = false;
                ShowSetupState();
            }
        }

        // ═══════════════════════════════════════════
        //  状態切り替え
        // ═══════════════════════════════════════════

        private void ShowTitleState()
        {
            _doc.TitleState.style.display = DisplayStyle.Flex;
            _doc.SetupState.style.display = DisplayStyle.None;
            _doc.SettingsOverlay.style.display = DisplayStyle.None;
            if (_doc.OnlineMenuState != null)
                _doc.OnlineMenuState.style.display = DisplayStyle.None;
            if (_doc.LobbyState != null)
                _doc.LobbyState.style.display = DisplayStyle.None;
        }

        private void ShowSetupState()
        {
            _doc.TitleState.style.display = DisplayStyle.None;
            _doc.SetupState.style.display = DisplayStyle.Flex;
            _doc.SettingsOverlay.style.display = DisplayStyle.None;
        }

        private void ShowOnlineMenuState()
        {
            _doc.TitleState.style.display = DisplayStyle.None;
            _doc.SetupState.style.display = DisplayStyle.None;
            if (_doc.OnlineMenuState != null)
                _doc.OnlineMenuState.style.display = DisplayStyle.Flex;
            if (_doc.LobbyState != null)
                _doc.LobbyState.style.display = DisplayStyle.None;
        }

        private void ShowLobbyState()
        {
            if (_doc.OnlineMenuState != null)
                _doc.OnlineMenuState.style.display = DisplayStyle.None;
            if (_doc.LobbyState != null)
                _doc.LobbyState.style.display = DisplayStyle.Flex;
        }

        private void ShowSettingsOverlay()
        {
            _doc.SettingsOverlay.style.display = DisplayStyle.Flex;
        }

        private void HideSettingsOverlay()
        {
            _doc.SettingsOverlay.style.display = DisplayStyle.None;
        }

        private void ShowCreditsOverlay()
        {
            if (_doc.CreditsText != null)
                _doc.CreditsText.text = GetCreditsText();
            _doc.CreditsOverlay.style.display = DisplayStyle.Flex;
        }

        private void HideCreditsOverlay()
        {
            _doc.CreditsOverlay.style.display = DisplayStyle.None;
        }

        private static string GetCreditsText()
        {
            return
@"FLOOR BREAKER

━━━━━━━━━━━━━━━━━━━━
  Game Engine
━━━━━━━━━━━━━━━━━━━━

Unity 6.3
(c) Unity Technologies


━━━━━━━━━━━━━━━━━━━━
  Third-Party Assets
━━━━━━━━━━━━━━━━━━━━

Feel v5.9.1
(c) More Mountains
https://feel.moremountains.com/

DOTween Pro
(c) 2014-2018 Daniele Giardini - Demigiant
http://dotween.demigiant.com

Epic Toon FX
(c) Archanor VFX

All In 1 Sprite Shader
(c) Seaside Game Studios

Medieval Fantasy SFX Bundle
(c) Leohpaz

Action RPG Music FREE
(c) Vertex Studio


━━━━━━━━━━━━━━━━━━━━
  Icons
━━━━━━━━━━━━━━━━━━━━

Upgrade icons by Lorc and Delapouite
Available on https://game-icons.net

Licensed under Creative Commons Attribution 3.0
https://creativecommons.org/licenses/by/3.0/


━━━━━━━━━━━━━━━━━━━━
  Open Source Libraries
━━━━━━━━━━━━━━━━━━━━

The following open source libraries are used
under the MIT License.

VContainer v1.17.0
Copyright (c) 2020 hadashiA
https://github.com/hadashiA/VContainer

UniTask v2.5.10
Copyright (c) 2019 Yoshifumi Kawai / Cysharp, Inc.
https://github.com/Cysharp/UniTask

R3
Copyright (c) 2024 Cysharp, Inc.
https://github.com/Cysharp/R3

NuGetForUnity
Copyright (c) 2018 Patrick McCarthy
https://github.com/GlitchEnzo/NuGetForUnity

── MIT License ──

Permission is hereby granted, free of charge, to
any person obtaining a copy of this software and
associated documentation files (the ""Software""),
to deal in the Software without restriction,
including without limitation the rights to use,
copy, modify, merge, publish, distribute,
sublicense, and/or sell copies of the Software,
and to permit persons to whom the Software is
furnished to do so, subject to the following
conditions:

The above copyright notice and this permission
notice shall be included in all copies or
substantial portions of the Software.

THE SOFTWARE IS PROVIDED ""AS IS"", WITHOUT
WARRANTY OF ANY KIND, EXPRESS OR IMPLIED,
INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE
AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS
OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM,
DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT
OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE
OR OTHER DEALINGS IN THE SOFTWARE.
";
        }

        // ═══════════════════════════════════════════
        //  プレイヤースロット
        // ═══════════════════════════════════════════

        private void SetupSlots()
        {
            // P1/P2: デフォルト Human + デフォルトデバイス割り当て
            _modeConfig.IsCpuSlot[0] = false;
            _modeConfig.IsCpuSlot[1] = false;
            _modeConfig.DeviceTypes[0] = DeviceType.KeyboardWasd;
            _modeConfig.DeviceTypes[1] = DeviceType.KeyboardArrows;

            for (int i = 0; i < 4; i++)
            {
                int slot = i; // capture
                _doc.SlotToggleButtons[i]?.RegisterCallback<ClickEvent>(_ => OnToggleSlot(slot));
                _doc.SlotAddButtons[i]?.RegisterCallback<ClickEvent>(_ => ExpandSlot(slot));
                if (_doc.SlotRemoveButtons[i] != null)
                    _doc.SlotRemoveButtons[i].RegisterCallback<ClickEvent>(_ => CollapseSlot(slot));

                // デバイスラベルクリックで再割り当て
                _doc.SlotDeviceLabels[i]?.RegisterCallback<ClickEvent>(_ =>
                {
                    if (!_modeConfig.IsCpuSlot[slot]) StartDeviceListening(slot);
                });
            }

            // 初期状態を反映
            RefreshAllSlotUI();
        }

        private void OnToggleSlot(int slot)
        {
            bool wasCpu = _modeConfig.IsCpuSlot[slot];
            _modeConfig.IsCpuSlot[slot] = !wasCpu;

            if (wasCpu)
            {
                // CPU → Human: デバイス割り当て待ちへ
                _modeConfig.ClearDevice(slot);
                StartDeviceListening(slot);
            }
            else
            {
                // Human → CPU: デバイス解放
                StopDeviceListening();
                _modeConfig.ClearDevice(slot);
            }

            RefreshSlotUI(slot);
            _audio?.PlaySfx(SfxIds.UiNavigate);
        }

        private void ExpandSlot(int slot)
        {
            _doc.SlotAddButtons[slot].style.display = DisplayStyle.None;
            _doc.SlotContents[slot].style.display = DisplayStyle.Flex;
            _doc.Slots[slot].RemoveFromClassList("setup-slot--empty");
            _doc.Slots[slot].AddToClassList("setup-slot--active");
            _modeConfig.IsCpuSlot[slot] = true;
            _modeConfig.ClearDevice(slot);
            RecalcPlayerCount();
            RefreshSlotUI(slot);
            _audio?.PlaySfx(SfxIds.UiNavigate);
        }

        private void CollapseSlot(int slot)
        {
            StopDeviceListening();
            _doc.SlotContents[slot].style.display = DisplayStyle.None;
            _doc.SlotAddButtons[slot].style.display = DisplayStyle.Flex;
            _doc.Slots[slot].RemoveFromClassList("setup-slot--active");
            _doc.Slots[slot].AddToClassList("setup-slot--empty");
            _modeConfig.IsCpuSlot[slot] = false;
            _modeConfig.ClearDevice(slot);
            RecalcPlayerCount();
            _audio?.PlaySfx(SfxIds.UiNavigate);
        }

        private void RecalcPlayerCount()
        {
            int count = 2; // P1 + P2 は常時
            for (int i = 2; i < 4; i++)
                if (_doc.SlotContents[i]?.resolvedStyle.display == DisplayStyle.Flex) count++;
            _modeConfig.PlayerCount = count;
        }

        // ── デバイス割り当て (Press to Join) ──

        private void StartDeviceListening(int slot)
        {
            _deviceDetection.StopListening();
            RefreshSlotUI(slot);
            _deviceDetection.StartListening(slot);
        }

        private void StopDeviceListening()
        {
            int prev = _deviceDetection.ListeningSlot;
            _deviceDetection.StopListening();
            if (prev >= 0) RefreshSlotUI(prev);
        }

        private void OnDeviceAssigned(int slot, DeviceType type, int gamepadIndex)
        {
            // 既に他スロットで使用中なら無視
            if (_modeConfig.IsDeviceTypeAssigned(type, slot, gamepadIndex)) return;

            _modeConfig.DeviceTypes[slot] = type;
            _modeConfig.GamepadIndices[slot] = gamepadIndex;
            RefreshSlotUI(slot);
            _audio?.PlaySfx(SfxIds.UiNavigate);
        }

        // ── スロット UI 更新 ──

        private void RefreshAllSlotUI()
        {
            for (int i = 0; i < 4; i++) RefreshSlotUI(i);
        }

        private void RefreshSlotUI(int slot)
        {
            var typeLabel = _doc.SlotTypeLabels[slot];
            var deviceLabel = _doc.SlotDeviceLabels[slot];
            var toggleBtn = _doc.SlotToggleButtons[slot];
            if (typeLabel == null) return;

            bool isCpu = _modeConfig.IsCpuSlot[slot];

            // タイプ表示
            typeLabel.text = isCpu ? "CPU" : "Human";
            typeLabel.EnableInClassList("setup-slot__type--cpu", isCpu);

            // トグルボタンテキスト
            if (toggleBtn != null)
                toggleBtn.text = isCpu ? "Human に変更" : "CPU に変更";

            // デバイス表示
            if (deviceLabel != null)
            {
                if (isCpu)
                {
                    deviceLabel.text = "";
                    deviceLabel.RemoveFromClassList("setup-slot__device--waiting");
                }
                else if (_deviceDetection.IsListening && _deviceDetection.ListeningSlot == slot)
                {
                    deviceLabel.text = "ボタンを押して割り当て";
                    deviceLabel.AddToClassList("setup-slot__device--waiting");
                }
                else
                {
                    deviceLabel.text = GetDeviceDisplayName(slot);
                    deviceLabel.RemoveFromClassList("setup-slot__device--waiting");
                }
            }
        }

        private string GetDeviceDisplayName(int slot)
        {
            var type = _modeConfig.DeviceTypes[slot];
            return type switch
            {
                DeviceType.KeyboardWasd => "Keyboard (WASD)",
                DeviceType.KeyboardArrows => "Keyboard (矢印)",
                DeviceType.Gamepad => _modeConfig.GamepadIndices[slot] >= 0
                    ? $"Gamepad {_modeConfig.GamepadIndices[slot] + 1}"
                    : "Gamepad",
                _ => "未割り当て",
            };
        }

        // ═══════════════════════════════════════════
        //  音量（ステージ選択は StageSelectUI に委譲）
        private void SetupVolumeSliders()
        {
            if (_audio == null) return;

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

        // ═══════════════════════════════════════════
        //  操作説明
        // ═══════════════════════════════════════════

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
