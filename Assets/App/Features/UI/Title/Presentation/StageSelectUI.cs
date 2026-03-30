using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using FloorBreaker.Shared.Application.Interfaces;
using FloorBreaker.MatchFlow.Application;
using FloorBreaker.ScriptableObjects.Configs;
using FloorBreaker.Stage.Domain;
using FloorBreaker.Stage.Presentation;

namespace FloorBreaker.UI.Title.Presentation
{
    /// <summary>
    /// ステージ選択 + プレビュー + ギミック詳細の UI ロジック。
    /// TitlePresenter と NetworkLobbyPresenter の両方から再利用される。
    /// ターゲット VisualElement をコンストラクタで受け取り、特定の UIDocument に依存しない。
    /// </summary>
    public sealed class StageSelectUI : IDisposable
    {
        private readonly VisualElement _stageListContainer;
        private readonly VisualElement _previewThumb;
        private readonly Label _previewName;
        private readonly Label _previewSize;
        private readonly Label _previewDesc;
        private readonly VisualElement _previewGimmicks;
        private readonly VisualElement _gimmickDetails;
        private readonly MatchModeConfig _modeConfig;
        private readonly TileSpriteConfig _tileSpriteConfig;
        private readonly Action<string> _onStageSelected;
        private readonly bool _isReadOnly;
        private readonly StagePreviewRenderer _previewRenderer;
        private readonly IRandomProvider _random;

        private readonly List<(VisualElement card, string assetName)> _stageCards = new();
        private readonly Dictionary<string, StageConfig> _stageConfigs = new();
        private IVisualElementScheduledItem _tickSchedule;

        /// <param name="stageListContainer">カード一覧の追加先</param>
        /// <param name="previewThumb">サムネイル表示先</param>
        /// <param name="previewName">ステージ名 Label</param>
        /// <param name="previewSize">サイズ Label</param>
        /// <param name="previewDesc">説明 Label</param>
        /// <param name="previewGimmicks">ギミックバッジ表示先 (null 許容)</param>
        /// <param name="gimmickDetails">ギミック詳細表示先 (null 許容)</param>
        /// <param name="modeConfig">選択結果の書き込み先</param>
        /// <param name="tileSpriteConfig">タイルの色・スプライト (null の場合 CSS フォールバック)</param>
        /// <param name="onStageSelected">選択時コールバック (SFX・sync 等)</param>
        /// <param name="isReadOnly">true の場合カードクリック無効</param>
        public StageSelectUI(
            VisualElement stageListContainer,
            VisualElement previewThumb,
            Label previewName,
            Label previewSize,
            Label previewDesc,
            VisualElement previewGimmicks,
            VisualElement gimmickDetails,
            MatchModeConfig modeConfig,
            TileSpriteConfig tileSpriteConfig = null,
            StagePreviewRenderer previewRenderer = null,
            IRandomProvider random = null,
            Action<string> onStageSelected = null,
            bool isReadOnly = false)
        {
            _stageListContainer = stageListContainer;
            _previewThumb = previewThumb;
            _previewName = previewName;
            _previewSize = previewSize;
            _previewDesc = previewDesc;
            _previewGimmicks = previewGimmicks;
            _gimmickDetails = gimmickDetails;
            _modeConfig = modeConfig;
            _tileSpriteConfig = tileSpriteConfig;
            _previewRenderer = previewRenderer;
            _random = random;
            _onStageSelected = onStageSelected;
            _isReadOnly = isReadOnly;
        }

        // ═══════════════════════════════════════════
        //  Public API
        // ═══════════════════════════════════════════

        public void PopulateStageList()
        {
            var configs = Resources.LoadAll<StageConfig>("StageConfigs");
            if (configs == null || configs.Length == 0) return;

            var sorted = configs.OrderBy(c => c.DisplayName).ToArray();

            foreach (var cfg in sorted)
            {
                _stageConfigs[cfg.name] = cfg;

                var card = new VisualElement();
                card.AddToClassList("stage-card");

                var thumb = new VisualElement();
                thumb.AddToClassList("stage-card__thumbnail");
                if (cfg.Thumbnail != null)
                    thumb.style.backgroundImage = new StyleBackground(cfg.Thumbnail);
                card.Add(thumb);

                var nameLabel = new Label(cfg.DisplayName);
                nameLabel.AddToClassList("stage-card__name");
                card.Add(nameLabel);

                string assetName = cfg.name;
                card.RegisterCallback<ClickEvent>(_ =>
                {
                    if (_isReadOnly) return;
                    SelectStage(assetName);
                });

                _stageListContainer.Add(card);
                _stageCards.Add((card, assetName));
            }

            // デフォルト選択
            string defaultName = !string.IsNullOrEmpty(_modeConfig.SelectedStageName)
                ? _modeConfig.SelectedStageName
                : sorted.FirstOrDefault(c => c.name == "Standard")?.name ?? sorted[0].name;
            SelectStageInternal(defaultName, notify: false);

            // ギミックシミュレーションの毎フレーム Tick
            if (_previewRenderer != null && _stageListContainer != null)
            {
                _tickSchedule = _stageListContainer.schedule.Execute(() =>
                {
                    _previewRenderer.Tick(0.016f); // ~60fps
                }).Every(16);
            }
        }

        /// <summary>ステージを選択する（コールバック発火あり）。</summary>
        public void SelectStage(string assetName)
        {
            SelectStageInternal(assetName, notify: true);
        }

        /// <summary>外部からの選択（コールバックなし）。クライアント側のネットワーク同期用。</summary>
        public void SelectStageWithoutCallback(string assetName)
        {
            SelectStageInternal(assetName, notify: false);
        }

        /// <summary>インデックスでステージを選択する。</summary>
        public void SelectByIndex(int index)
        {
            if (index < 0 || index >= _stageCards.Count) return;
            SelectStageInternal(_stageCards[index].assetName, notify: true);
        }

        /// <summary>カード要素の一覧を返す。</summary>
        public VisualElement[] GetCardElements()
        {
            var result = new VisualElement[_stageCards.Count];
            for (int i = 0; i < _stageCards.Count; i++)
                result[i] = _stageCards[i].card;
            return result;
        }

        public void Dispose()
        {
            _tickSchedule?.Pause();
            _previewRenderer?.Dispose();
        }

        // ═══════════════════════════════════════════
        //  Private: 選択 + プレビュー
        // ═══════════════════════════════════════════

        private void SelectStageInternal(string assetName, bool notify)
        {
            _modeConfig.SelectedStageName = assetName;

            foreach (var (card, name) in _stageCards)
            {
                if (name == assetName)
                    card.AddToClassList("stage-card--selected");
                else
                    card.RemoveFromClassList("stage-card--selected");
            }

            if (_stageConfigs.TryGetValue(assetName, out var cfg))
                UpdateStagePreview(cfg);

            if (notify)
                _onStageSelected?.Invoke(assetName);
        }

        private void UpdateStagePreview(StageConfig cfg)
        {
            // RenderTexture ベースのプレビュー
            if (_previewThumb != null)
            {
                var rt = _previewRenderer.RenderStagePreview(cfg, _random);
                _previewThumb.style.backgroundImage = Background.FromRenderTexture(rt);
            }

            if (_previewName != null) _previewName.text = cfg.DisplayName;
            if (_previewSize != null) _previewSize.text = $"{cfg.Width} x {cfg.Height}";
            if (_previewDesc != null) _previewDesc.text = cfg.Description;

            // ギミック検知
            var gimmickFlags = StageGimmickDetector.Detect(cfg.GasVeinCount, cfg.PresetTiles);
            bool hasGas = gimmickFlags.HasFlag(GimmickFlags.Gas);
            bool hasBedrock = gimmickFlags.HasFlag(GimmickFlags.Bedrock);
            bool hasWarp = gimmickFlags.HasFlag(GimmickFlags.Warp);
            bool hasEternalFire = gimmickFlags.HasFlag(GimmickFlags.EternalFire);

            // ギミックバッジ
            if (_previewGimmicks != null)
            {
                _previewGimmicks.Clear();
                if (hasGas) AddGimmickBadge("ガス", "gimmick-badge--gas");
                if (hasBedrock) AddGimmickBadge("岩盤", "gimmick-badge--bedrock");
                if (hasWarp) AddGimmickBadge("ワープ", "gimmick-badge--warp");
                if (hasEternalFire) AddGimmickBadge("永久炎", "gimmick-badge--eternal-fire");
                if (!hasGas && !hasBedrock && !hasWarp && !hasEternalFire)
                    AddGimmickBadge("ギミックなし", "");
            }

            // ギミック詳細
            if (_gimmickDetails != null)
                UpdateGimmickDetails(hasGas, hasBedrock, hasWarp, hasEternalFire);
        }

        private void AddGimmickBadge(string text, string extraClass)
        {
            var badge = new Label(text);
            badge.AddToClassList("gimmick-badge");
            if (!string.IsNullOrEmpty(extraClass))
                badge.AddToClassList(extraClass);
            _previewGimmicks.Add(badge);
        }

        // ═══════════════════════════════════════════
        //  ギミック詳細 + ループプレビュー
        // ═══════════════════════════════════════════

        private void UpdateGimmickDetails(bool hasGas, bool hasBedrock, bool hasWarp, bool hasEternalFire)
        {
            _gimmickDetails.Clear();
            _previewRenderer?.ClearGimmickSimulations();

            AddRenderedGimmickDetail(hasGas, GimmickType.Gas, "ガスタイル",
                "炎が引火すると周囲のガスに連鎖延焼する。戦略的に利用しよう。",
                "gimmick-detail__name--gas");
            AddRenderedGimmickDetail(hasBedrock, GimmickType.Bedrock, "岩盤",
                "破壊不能な壁。ボムの爆風も貫通しない。通路を形成する。",
                "gimmick-detail__name--bedrock");
            AddRenderedGimmickDetail(hasWarp, GimmickType.Warp, "ワープタイル",
                "踏むとペアのタイルに瞬間移動。壁越しの移動が可能。",
                "gimmick-detail__name--warp");
            AddRenderedGimmickDetail(hasEternalFire, GimmickType.EternalFire, "永久炎",
                "消えない青い炎。触れるとダメージ。回避必須の危険地帯。",
                "gimmick-detail__name--eternal-fire");
        }

        private void AddRenderedGimmickDetail(
            bool hasGimmick, GimmickType type, string title, string description, string nameClass)
        {
            if (!hasGimmick || _previewRenderer == null) return;

            var detail = new VisualElement();
            detail.AddToClassList("gimmick-detail");

            var rt = _previewRenderer.RenderGimmickPreview(type);
            var preview = new VisualElement();
            preview.AddToClassList("gimmick-detail__preview");
            preview.style.backgroundImage = Background.FromRenderTexture(rt);
            detail.Add(preview);

            var textWrap = new VisualElement();
            textWrap.AddToClassList("gimmick-detail__text");

            var name = new Label(title);
            name.AddToClassList("gimmick-detail__name");
            name.AddToClassList(nameClass);
            textWrap.Add(name);

            var desc = new Label(description);
            desc.AddToClassList("gimmick-detail__desc");
            textWrap.Add(desc);

            detail.Add(textWrap);

            _gimmickDetails.Add(detail);
        }
    }
}
