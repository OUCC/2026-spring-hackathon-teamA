using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;
using FloorBreaker.Shared.Application.Interfaces;
using FloorBreaker.Input.Infrastructure;
using FloorBreaker.MatchFlow.Application;
using FloorBreaker.ScriptableObjects.Configs;
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

        // ステージ選択状態
        private readonly List<(VisualElement card, string assetName)> _stageCards = new();
        private readonly Dictionary<string, StageConfig> _stageConfigs = new();
        private readonly List<IVisualElementScheduledItem> _gimmickAnimations = new();
        private string _selectedStageName;

        public TitlePresenter(
            TitleUIDocument doc,
            IAudioService audio,
            KeyRebindingService rebindService,
            MatchModeConfig modeConfig,
            ISceneTransitionService sceneTransition,
            DeviceDetectionService deviceDetection = null)
        {
            _doc = doc;
            _audio = audio;
            _modeConfig = modeConfig;
            _rebindService = rebindService;
            _deviceDetection = deviceDetection ?? new DeviceDetectionService();
            _deviceDetection.OnDeviceAssigned += OnDeviceAssigned;

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
            PopulateStageList();

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
        }

        private void ShowSetupState()
        {
            _doc.TitleState.style.display = DisplayStyle.None;
            _doc.SetupState.style.display = DisplayStyle.Flex;
            _doc.SettingsOverlay.style.display = DisplayStyle.None;
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
        //  ステージ選択
        // ═══════════════════════════════════════════

        private void PopulateStageList()
        {
            var configs = Resources.LoadAll<StageConfig>("StageConfigs");
            if (configs == null || configs.Length == 0) return;

            var sorted = configs.OrderBy(c => c.DisplayName).ToArray();

            foreach (var cfg in sorted)
            {
                _stageConfigs[cfg.name] = cfg;

                var card = new VisualElement();
                card.AddToClassList("stage-card");

                // 小サムネイル
                var thumb = new VisualElement();
                thumb.AddToClassList("stage-card__thumbnail");
                if (cfg.Thumbnail != null)
                    thumb.style.backgroundImage = new StyleBackground(cfg.Thumbnail);
                card.Add(thumb);

                // ステージ名
                var nameLabel = new Label(cfg.DisplayName);
                nameLabel.AddToClassList("stage-card__name");
                card.Add(nameLabel);

                string assetName = cfg.name;
                card.RegisterCallback<ClickEvent>(_ => SelectStage(assetName));

                _doc.StageList.Add(card);
                _stageCards.Add((card, assetName));
            }

            // デフォルト選択
            string defaultName = !string.IsNullOrEmpty(_modeConfig.SelectedStageName)
                ? _modeConfig.SelectedStageName
                : sorted.FirstOrDefault(c => c.name == "Standard")?.name ?? sorted[0].name;
            SelectStage(defaultName);
        }

        private void SelectStage(string assetName)
        {
            _selectedStageName = assetName;
            _modeConfig.SelectedStageName = assetName;

            // リスト選択状態更新
            foreach (var (card, name) in _stageCards)
            {
                if (name == assetName)
                    card.AddToClassList("stage-card--selected");
                else
                    card.RemoveFromClassList("stage-card--selected");
            }

            // プレビューパネル更新
            if (_stageConfigs.TryGetValue(assetName, out var cfg))
                UpdateStagePreview(cfg);

            _audio?.PlaySfx(SfxIds.UiNavigate);
        }

        private void UpdateStagePreview(StageConfig cfg)
        {
            // サムネイル
            if (cfg.Thumbnail != null)
                _doc.StagePreviewThumb.style.backgroundImage = new StyleBackground(cfg.Thumbnail);

            _doc.StagePreviewName.text = cfg.DisplayName;
            _doc.StagePreviewSize.text = $"{cfg.Width} x {cfg.Height}";
            _doc.StagePreviewDesc.text = cfg.Description;

            // ギミックバッジ
            _doc.StagePreviewGimmicks.Clear();

            bool hasGas = cfg.GasVeinCount > 0;
            bool hasBedrock = false;
            bool hasWarp = false;
            bool hasEternalFire = false;

            if (cfg.PresetTiles != null)
            {
                foreach (var p in cfg.PresetTiles)
                {
                    if (p.type == Stage.Domain.TileType.Bedrock) hasBedrock = true;
                    if (p.type == Stage.Domain.TileType.Warp) hasWarp = true;
                    if (p.type == Stage.Domain.TileType.Gas) hasGas = true;
                    if (p.condition == Stage.Domain.TileCondition.EternalFire) hasEternalFire = true;
                }
            }

            if (hasGas) AddGimmickBadge("ガス", "gimmick-badge--gas");
            if (hasBedrock) AddGimmickBadge("岩盤", "gimmick-badge--bedrock");
            if (hasWarp) AddGimmickBadge("ワープ", "gimmick-badge--warp");
            if (hasEternalFire) AddGimmickBadge("永久炎", "gimmick-badge--eternal-fire");
            if (!hasGas && !hasBedrock && !hasWarp && !hasEternalFire)
                AddGimmickBadge("ギミックなし", "");

            // ギミック詳細パネル
            UpdateGimmickDetails(hasGas, hasBedrock, hasWarp, hasEternalFire);
        }

        private void AddGimmickBadge(string text, string extraClass)
        {
            var badge = new Label(text);
            badge.AddToClassList("gimmick-badge");
            if (!string.IsNullOrEmpty(extraClass))
                badge.AddToClassList(extraClass);
            _doc.StagePreviewGimmicks.Add(badge);
        }

        // ═══════════════════════════════════════════
        //  ギミック詳細 + ループプレビュー
        // ═══════════════════════════════════════════

        private void UpdateGimmickDetails(bool hasGas, bool hasBedrock, bool hasWarp, bool hasEternalFire)
        {
            // 旧アニメーション停止
            foreach (var anim in _gimmickAnimations) anim?.Pause();
            _gimmickAnimations.Clear();
            _doc.GimmickDetails.Clear();

            if (hasGas) AddGimmickDetail_Gas();
            if (hasBedrock) AddGimmickDetail_Bedrock();
            if (hasWarp) AddGimmickDetail_Warp();
            if (hasEternalFire) AddGimmickDetail_EternalFire();
        }

        private VisualElement CreateMiniGrid(int size, string[,] tileClasses)
        {
            var grid = new VisualElement();
            grid.AddToClassList("gimmick-detail__grid");
            for (int y = size - 1; y >= 0; y--)
                for (int x = 0; x < size; x++)
                {
                    var tile = new VisualElement();
                    tile.AddToClassList("gimmick-tile");
                    tile.AddToClassList(tileClasses[x, y]);
                    tile.name = $"gt_{x}_{y}";
                    grid.Add(tile);
                }
            return grid;
        }

        private void AddGimmickDetail_Gas()
        {
            var detail = new VisualElement();
            detail.AddToClassList("gimmick-detail");

            // 5x5 グリッド: 中央に Gas ライン
            var classes = new string[5, 5];
            for (int x = 0; x < 5; x++)
                for (int y = 0; y < 5; y++)
                    classes[x, y] = "gimmick-tile--normal";
            // Gas ライン: y=2 横一列
            for (int x = 0; x < 5; x++) classes[x, 2] = "gimmick-tile--gas";
            // Gas 縦: x=2, y=0..1
            classes[2, 0] = "gimmick-tile--gas";
            classes[2, 1] = "gimmick-tile--gas";

            var grid = CreateMiniGrid(5, classes);
            detail.Add(grid);

            var name = new Label("ガスタイル");
            name.AddToClassList("gimmick-detail__name");
            name.AddToClassList("gimmick-detail__name--gas");
            detail.Add(name);

            var desc = new Label("炎が引火すると周囲のガスに連鎖延焼する。戦略的に利用しよう。");
            desc.AddToClassList("gimmick-detail__desc");
            detail.Add(desc);

            _doc.GimmickDetails.Add(detail);

            // アニメ: 炎が左から順に延焼
            int step = 0;
            var anim = grid.schedule.Execute(() =>
            {
                // リセット
                for (int x = 0; x < 5; x++)
                {
                    var t = grid.Q($"gt_{x}_2");
                    t?.RemoveFromClassList("gimmick-tile--gas-fire");
                    t?.AddToClassList("gimmick-tile--gas");
                }
                var tv = grid.Q($"gt_2_0"); tv?.RemoveFromClassList("gimmick-tile--gas-fire"); tv?.AddToClassList("gimmick-tile--gas");
                tv = grid.Q($"gt_2_1"); tv?.RemoveFromClassList("gimmick-tile--gas-fire"); tv?.AddToClassList("gimmick-tile--gas");

                // 現在ステップまで延焼
                int[] orderX = { 0, 1, 2, 3, 4, 2, 2 };
                int[] orderY = { 2, 2, 2, 2, 2, 1, 0 };
                for (int i = 0; i <= step && i < orderX.Length; i++)
                {
                    var t = grid.Q($"gt_{orderX[i]}_{orderY[i]}");
                    t?.RemoveFromClassList("gimmick-tile--gas");
                    t?.AddToClassList("gimmick-tile--gas-fire");
                }
                step = (step + 1) % (orderX.Length + 3); // 3フレーム空白
            }).Every(400);
            _gimmickAnimations.Add(anim);
        }

        private void AddGimmickDetail_Bedrock()
        {
            var detail = new VisualElement();
            detail.AddToClassList("gimmick-detail");

            var classes = new string[5, 5];
            for (int x = 0; x < 5; x++)
                for (int y = 0; y < 5; y++)
                    classes[x, y] = "gimmick-tile--normal";
            // Bedrock 壁: x=2 縦一列
            for (int y = 0; y < 5; y++) classes[2, y] = "gimmick-tile--bedrock";

            var grid = CreateMiniGrid(5, classes);
            // ボム範囲表示 (静的): x=1 が赤く光って壁で止まる
            var bombTile = grid.Q("gt_1_2");
            bombTile?.AddToClassList("gimmick-tile--bomb-range");
            detail.Add(grid);

            var name = new Label("岩盤");
            name.AddToClassList("gimmick-detail__name");
            name.AddToClassList("gimmick-detail__name--bedrock");
            detail.Add(name);

            var desc = new Label("破壊不能な壁。ボムの爆風も貫通しない。通路を形成する。");
            desc.AddToClassList("gimmick-detail__desc");
            detail.Add(desc);

            _doc.GimmickDetails.Add(detail);
            // Bedrock はアニメなし
        }

        private void AddGimmickDetail_Warp()
        {
            var detail = new VisualElement();
            detail.AddToClassList("gimmick-detail");

            var classes = new string[5, 5];
            for (int x = 0; x < 5; x++)
                for (int y = 0; y < 5; y++)
                    classes[x, y] = "gimmick-tile--normal";
            // Warp ペア
            classes[1, 2] = "gimmick-tile--warp";
            classes[3, 2] = "gimmick-tile--warp";

            var grid = CreateMiniGrid(5, classes);
            detail.Add(grid);

            var name = new Label("ワープタイル");
            name.AddToClassList("gimmick-detail__name");
            name.AddToClassList("gimmick-detail__name--warp");
            detail.Add(name);

            var desc = new Label("踏むとペアのタイルに瞬間移動。壁越しの移動が可能。");
            desc.AddToClassList("gimmick-detail__desc");
            detail.Add(desc);

            _doc.GimmickDetails.Add(detail);

            // アニメ: プレイヤーが左ワープに入り右ワープから出る
            int step = 0;
            var anim = grid.schedule.Execute(() =>
            {
                var warpA = grid.Q("gt_1_2");
                var warpB = grid.Q("gt_3_2");
                var approachTile = grid.Q("gt_0_2");
                var exitTile = grid.Q("gt_4_2");

                // リセット
                approachTile?.RemoveFromClassList("gimmick-tile--player");
                warpA?.RemoveFromClassList("gimmick-tile--warp-flash");
                warpB?.RemoveFromClassList("gimmick-tile--warp-flash");
                exitTile?.RemoveFromClassList("gimmick-tile--player");
                approachTile?.AddToClassList("gimmick-tile--normal");
                exitTile?.AddToClassList("gimmick-tile--normal");

                switch (step % 6)
                {
                    case 0: // プレイヤーが左に近づく
                        approachTile?.RemoveFromClassList("gimmick-tile--normal");
                        approachTile?.AddToClassList("gimmick-tile--player");
                        break;
                    case 1: // ワープAに入る (フラッシュ)
                        warpA?.AddToClassList("gimmick-tile--warp-flash");
                        break;
                    case 2: // ワープBから出る (フラッシュ)
                        warpA?.RemoveFromClassList("gimmick-tile--warp-flash");
                        warpB?.AddToClassList("gimmick-tile--warp-flash");
                        break;
                    case 3: // 出口タイルにプレイヤー
                        warpB?.RemoveFromClassList("gimmick-tile--warp-flash");
                        exitTile?.RemoveFromClassList("gimmick-tile--normal");
                        exitTile?.AddToClassList("gimmick-tile--player");
                        break;
                    case 4: // クリア
                    case 5:
                        break;
                }
                step++;
            }).Every(500);
            _gimmickAnimations.Add(anim);
        }

        private void AddGimmickDetail_EternalFire()
        {
            var detail = new VisualElement();
            detail.AddToClassList("gimmick-detail");

            var classes = new string[5, 5];
            for (int x = 0; x < 5; x++)
                for (int y = 0; y < 5; y++)
                    classes[x, y] = "gimmick-tile--normal";
            // 中央3x3 EternalFire
            for (int x = 1; x <= 3; x++)
                for (int y = 1; y <= 3; y++)
                    classes[x, y] = "gimmick-tile--eternal-fire";

            var grid = CreateMiniGrid(5, classes);
            detail.Add(grid);

            var name = new Label("永久炎");
            name.AddToClassList("gimmick-detail__name");
            name.AddToClassList("gimmick-detail__name--eternal-fire");
            detail.Add(name);

            var desc = new Label("消えない青い炎。触れるとダメージ。回避必須の危険地帯。");
            desc.AddToClassList("gimmick-detail__desc");
            detail.Add(desc);

            _doc.GimmickDetails.Add(detail);

            // アニメ: パルス明滅
            bool bright = false;
            var anim = grid.schedule.Execute(() =>
            {
                for (int x = 1; x <= 3; x++)
                    for (int y = 1; y <= 3; y++)
                    {
                        var t = grid.Q($"gt_{x}_{y}");
                        if (bright)
                        {
                            t?.RemoveFromClassList("gimmick-tile--eternal-fire-pulse");
                            t?.AddToClassList("gimmick-tile--eternal-fire");
                        }
                        else
                        {
                            t?.RemoveFromClassList("gimmick-tile--eternal-fire");
                            t?.AddToClassList("gimmick-tile--eternal-fire-pulse");
                        }
                    }
                bright = !bright;
            }).Every(600);
            _gimmickAnimations.Add(anim);
        }

        // ═══════════════════════════════════════════
        //  音量
        // ═══════════════════════════════════════════

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
