using System;
using System.Collections.Generic;
using R3;
using UnityEngine.UIElements;
using FloorBreaker.Shared.Domain.Primitives;
using FloorBreaker.Shared.Domain.Timing;
using FloorBreaker.Player.Domain;
using FloorBreaker.Upgrades.Domain;
using FloorBreaker.Upgrades.Application;
using FloorBreaker.Shared.Application.Interfaces;
using FloorBreaker.MatchFlow.Application;
using FloorBreaker.UI.RuntimeUI.Controls;

namespace FloorBreaker.UI.UpgradeOverlay.Presentation
{
    /// <summary>
    /// 強化オーバーレイを駆動する Presenter。
    /// </summary>
    public sealed class UpgradeOverlayPresenter : IDisposable
    {
        private readonly UpgradeOverlayView _view;
        private readonly UpgradePhaseUseCase _upgradePhase;
        private readonly UpgradeSelectionState _selectionState;
        private readonly VisualTreeAsset _cardTemplate;
        private readonly UpgradeIconMap _iconMap;
        private readonly IReadOnlyList<PlayerStats> _playerStats;
        private readonly IAudioService _audio;
        private readonly int[] _humanIndices;

        /// <summary>Card elements per player pane index.</summary>
        private readonly List<UpgradeCardElement>[] _cardElements;
        private readonly List<IDisposable> _subscriptions = new();
        private int _lastPulseSecond = -1;

        /// <summary>Number of visible panes (min of player count and UXML pane count).</summary>
        private readonly int _visiblePaneCount;

        public UpgradeOverlayPresenter(
            UpgradeOverlayView view,
            MatchClock clock,
            UpgradePhaseUseCase upgradePhase,
            UpgradeSelectionState selectionState,
            IReadOnlyList<PlayerStats> playerStats,
            VisualTreeAsset cardTemplate,
            IAudioService audio = null,
            int[] humanIndices = null,
            UpgradeIconMap iconMap = null)
        {
            _view = view;
            _upgradePhase = upgradePhase;
            _selectionState = selectionState;
            _cardTemplate = cardTemplate;
            _iconMap = iconMap;
            _playerStats = playerStats;
            _audio = audio;
            _humanIndices = humanIndices;

            _visiblePaneCount = Math.Min(playerStats.Count, view.PaneCount);

            // Initialize card element lists per pane
            _cardElements = new List<UpgradeCardElement>[_visiblePaneCount];
            for (int i = 0; i < _visiblePaneCount; i++)
                _cardElements[i] = new List<UpgradeCardElement>();

            // Phase visibility
            _subscriptions.Add(clock.CurrentPhase.Subscribe(phase =>
            {
                if (phase == GamePhase.UpgradePhase)
                    _view.Show();
                else
                    _view.Hide();
            }));

            // Per-player subscriptions (pane → player mapping via humanIndices)
            for (int i = 0; i < _visiblePaneCount; i++)
            {
                int paneIndex = i; // capture for closures
                int playerIndex = _humanIndices != null ? _humanIndices[i] : i;
                var playerId = PlayerId.FromIndex(playerIndex);
                var draft = upgradePhase.GetDraft(playerId);
                var stats = playerStats[i];

                // Card choices
                _subscriptions.Add(draft.CurrentChoices.Subscribe(
                    choices => PopulateCards(_view.GetCards(paneIndex), choices, _cardElements[paneIndex], stats, playerId)));

                // Draft state
                _subscriptions.Add(draft.State.Subscribe(state =>
                {
                    _view.SetStatus(paneIndex, GetStatusText(state));
                    _view.SetDone(paneIndex, state != DraftState.Choosing);
                    SetCardsDone(_cardElements[paneIndex], state != DraftState.Choosing);
                    if (state == DraftState.Skipped) _audio?.PlaySfx(SfxIds.UpgradeDone);
                }));

                // Card index + reroll highlight + navigation SE
                _subscriptions.Add(selectionState.GetIndexObservable(playerId).Subscribe(_ =>
                {
                    RefreshHighlight(playerId, paneIndex, _cardElements[paneIndex]);
                    _audio?.PlaySfx(SfxIds.UiNavigate);
                }));

                // Purchase notification
                _subscriptions.Add(selectionState.GetPurchaseCountObservable(playerId).Subscribe(count =>
                {
                    RefreshCardStates(_cardElements[paneIndex], playerId, stats);
                    if (count > 0) _audio?.PlaySfx(SfxIds.UpgradeSelect);
                }));

                // Row switch: card row (row=0) <-> done row (row=1)
                _subscriptions.Add(selectionState.GetRowObservable(playerId).Subscribe(
                    _ => RefreshHighlight(playerId, paneIndex, _cardElements[paneIndex])));
            }
        }

        /// <summary>死亡した Human プレイヤーのペインを永久非表示にする。</summary>
        public void DisablePane(int paneIndex)
        {
            _view.HidePane(paneIndex);
        }

        public void UpdateCountdown()
        {
            if (!_upgradePhase.IsActive)
            {
                _lastPulseSecond = -1;
                return;
            }
            int seconds = (int)MathF.Ceiling(_upgradePhase.RemainingTime.CurrentValue);
            _view.SetCountdown(seconds);

            // 残り 3 秒以下でパルス演出
            if (seconds <= 3 && seconds > 0 && seconds != _lastPulseSecond)
            {
                _lastPulseSecond = seconds;
                _view.PulseCountdown();
                _audio?.PlaySfx(SfxIds.CountdownTick);
            }
        }

        public void Dispose()
        {
            foreach (var sub in _subscriptions)
                sub.Dispose();
            _subscriptions.Clear();
        }

        private void PopulateCards(
            VisualElement container,
            IReadOnlyList<UpgradeDefinition> choices,
            List<UpgradeCardElement> cardElements,
            PlayerStats stats,
            PlayerId player)
        {
            container.Clear();
            cardElements.Clear();

            for (int i = 0; i < choices.Count; i++)
            {
                var def = choices[i];
                var card = new UpgradeCardElement(_cardTemplate);
                card.SetData(def, _iconMap);

                bool purchased = _selectionState.IsPurchased(player, i);
                card.SetLocked(!purchased && stats.Coins.CurrentValue < def.Cost);
                card.SetDone(purchased);

                container.Add(card.Root);
                cardElements.Add(card);
            }

            // カード生成後に初期ハイライトを適用（index 0 = 左カード）
            if (cardElements.Count > 0)
            {
                UpdateSelection(cardElements, 0, true);
            }
        }

        private static void UpdateSelection(List<UpgradeCardElement> cards, int selectedIndex, bool isCardRow)
        {
            for (int i = 0; i < cards.Count; i++)
                cards[i].SetSelected(isCardRow && i == selectedIndex);
        }

        private void RefreshHighlight(PlayerId player, int paneIndex, List<UpgradeCardElement> cards)
        {
            int row = _selectionState.GetRow(player);
            int idx = _selectionState.GetIndex(player);
            bool onCardRow = row == 0;
            bool onReroll = onCardRow && idx == 3;
            bool onCard = onCardRow && idx < 3;

            // カードハイライト
            UpdateSelection(cards, onCard ? idx : -1, true);

            // リロール・完了ハイライト
            _view.SetRerollHighlight(paneIndex, onReroll);
            _view.SetDoneHighlight(paneIndex, row == 1);
        }

        private void RefreshCardStates(List<UpgradeCardElement> cards, PlayerId player, PlayerStats stats)
        {
            var choices = _upgradePhase.GetDraft(player).CurrentChoices.CurrentValue;

            for (int i = 0; i < cards.Count && i < choices.Count; i++)
            {
                bool purchased = _selectionState.IsPurchased(player, i);
                cards[i].SetDone(purchased);
                cards[i].SetLocked(!purchased && stats.Coins.CurrentValue < choices[i].Cost);
                cards[i].SetSelected(false);
            }

            // 現在の選択ハイライトを再適用
            int row = _selectionState.GetRow(player);
            if (row == 0)
            {
                int idx = _selectionState.GetIndex(player);
                UpdateSelection(cards, idx, true);
            }
        }

        private static void SetCardsDone(List<UpgradeCardElement> cards, bool done)
        {
            foreach (var card in cards)
                card.SetDone(done);
        }

        private static string GetStatusText(DraftState state)
        {
            return state switch
            {
                DraftState.Choosing => "",
                DraftState.Selected => "選択完了",
                DraftState.Skipped => "購入完了",
                DraftState.TimedOut => "タイムアウト",
                _ => "",
            };
        }
    }
}
