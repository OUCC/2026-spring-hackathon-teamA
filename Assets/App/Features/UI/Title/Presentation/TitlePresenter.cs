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
            ISceneTransitionService sceneTransition)
        {
            _doc = doc;
            _audio = audio;
            _modeConfig = modeConfig;
            _rebindService = rebindService;

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
                sceneTransition.LoadMatchAsync().Forget();
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

        // ═══════════════════════════════════════════
        //  プレイヤースロット
        // ═══════════════════════════════════════════

        private void SetupSlots()
        {
            // P2 トグル
            _doc.SlotToggleP2?.RegisterCallback<ClickEvent>(_ =>
                ToggleSlot(_doc.SlotToggleP2, 1));

            // P3 追加/削除/トグル
            _doc.SlotAddP3?.RegisterCallback<ClickEvent>(_ => ExpandSlot(3));
            _doc.SlotRemoveP3?.RegisterCallback<ClickEvent>(_ => CollapseSlot(3));
            _doc.SlotToggleP3?.RegisterCallback<ClickEvent>(_ =>
                ToggleSlot(_doc.SlotToggleP3, 2));

            // P4 追加/削除/トグル
            _doc.SlotAddP4?.RegisterCallback<ClickEvent>(_ => ExpandSlot(4));
            _doc.SlotRemoveP4?.RegisterCallback<ClickEvent>(_ => CollapseSlot(4));
            _doc.SlotToggleP4?.RegisterCallback<ClickEvent>(_ =>
                ToggleSlot(_doc.SlotToggleP4, 3));
        }

        private void ToggleSlot(Button toggleBtn, int slotIndex)
        {
            _modeConfig.IsCpuSlot[slotIndex] = !_modeConfig.IsCpuSlot[slotIndex];
            toggleBtn.text = _modeConfig.IsCpuSlot[slotIndex] ? "CPU" : "Human";
            _audio?.PlaySfx(SfxIds.UiNavigate);
        }

        private void ExpandSlot(int playerNum)
        {
            var (slot, content, addBtn) = GetSlotElements(playerNum);
            addBtn.style.display = DisplayStyle.None;
            content.style.display = DisplayStyle.Flex;
            slot.RemoveFromClassList("setup-slot--empty");
            slot.AddToClassList("setup-slot--active");
            _modeConfig.IsCpuSlot[playerNum - 1] = true;
            var toggle = playerNum == 3 ? _doc.SlotToggleP3 : _doc.SlotToggleP4;
            if (toggle != null) toggle.text = "CPU";
            RecalcPlayerCount();
            _audio?.PlaySfx(SfxIds.UiNavigate);
        }

        private void CollapseSlot(int playerNum)
        {
            var (slot, content, addBtn) = GetSlotElements(playerNum);
            content.style.display = DisplayStyle.None;
            addBtn.style.display = DisplayStyle.Flex;
            slot.RemoveFromClassList("setup-slot--active");
            slot.AddToClassList("setup-slot--empty");
            _modeConfig.IsCpuSlot[playerNum - 1] = false;
            RecalcPlayerCount();
            _audio?.PlaySfx(SfxIds.UiNavigate);
        }

        private (VisualElement slot, VisualElement content, Button addBtn) GetSlotElements(int playerNum)
        {
            return playerNum == 3
                ? (_doc.SlotP3, _doc.SlotContentP3, _doc.SlotAddP3)
                : (_doc.SlotP4, _doc.SlotContentP4, _doc.SlotAddP4);
        }

        private void RecalcPlayerCount()
        {
            int count = 2; // P1 + P2 は常時
            if (_doc.SlotContentP3?.resolvedStyle.display == DisplayStyle.Flex) count++;
            if (_doc.SlotContentP4?.resolvedStyle.display == DisplayStyle.Flex) count++;
            _modeConfig.PlayerCount = count;
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
