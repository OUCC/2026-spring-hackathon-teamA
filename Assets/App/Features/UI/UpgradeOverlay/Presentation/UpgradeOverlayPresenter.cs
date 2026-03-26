using System;
using System.Collections.Generic;
using R3;
using UnityEngine.UIElements;
using FloorBreaker.Shared.Domain.Primitives;
using FloorBreaker.Shared.Domain.Timing;
using FloorBreaker.Player.Domain;
using FloorBreaker.Upgrades.Domain;
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
        private readonly PlayerStats _p1Stats;
        private readonly PlayerStats _p2Stats;

        private readonly List<UpgradeCardElement> _leftCardElements = new();
        private readonly List<UpgradeCardElement> _rightCardElements = new();
        private readonly List<IDisposable> _subscriptions = new();

        public UpgradeOverlayPresenter(
            UpgradeOverlayView view,
            MatchClock clock,
            UpgradePhaseUseCase upgradePhase,
            UpgradeSelectionState selectionState,
            PlayerStats p1Stats,
            PlayerStats p2Stats,
            VisualTreeAsset cardTemplate)
        {
            _view = view;
            _upgradePhase = upgradePhase;
            _selectionState = selectionState;
            _cardTemplate = cardTemplate;
            _p1Stats = p1Stats;
            _p2Stats = p2Stats;

            _subscriptions.Add(clock.CurrentPhase.Subscribe(phase =>
            {
                if (phase == GamePhase.UpgradePhase)
                    _view.Show();
                else
                    _view.Hide();
            }));

            _subscriptions.Add(upgradePhase.DraftP1.CurrentChoices.Subscribe(
                choices => PopulateCards(_view.LeftCards, choices, _leftCardElements, p1Stats, PlayerId.Player1)));

            _subscriptions.Add(upgradePhase.DraftP2.CurrentChoices.Subscribe(
                choices => PopulateCards(_view.RightCards, choices, _rightCardElements, p2Stats, PlayerId.Player2)));

            _subscriptions.Add(upgradePhase.DraftP1.State.Subscribe(state =>
            {
                _view.SetLeftStatus(GetStatusText(state));
                _view.SetLeftDone(state != DraftState.Choosing);
                SetCardsDone(_leftCardElements, state != DraftState.Choosing);
            }));

            _subscriptions.Add(upgradePhase.DraftP2.State.Subscribe(state =>
            {
                _view.SetRightStatus(GetStatusText(state));
                _view.SetRightDone(state != DraftState.Choosing);
                SetCardsDone(_rightCardElements, state != DraftState.Choosing);
            }));

            // カード行 + リロールの選択ハイライト
            _subscriptions.Add(selectionState.P1Index.Subscribe(
                _ => RefreshHighlight(PlayerId.Player1, _leftCardElements)));
            _subscriptions.Add(selectionState.P2Index.Subscribe(
                _ => RefreshHighlight(PlayerId.Player2, _rightCardElements)));

            // 購入通知 → カードの見た目を更新
            _subscriptions.Add(selectionState.P1PurchaseCount.Subscribe(
                _ => RefreshCardStates(_leftCardElements, PlayerId.Player1, p1Stats)));
            _subscriptions.Add(selectionState.P2PurchaseCount.Subscribe(
                _ => RefreshCardStates(_rightCardElements, PlayerId.Player2, p2Stats)));

            // 行切替: カード行 (row=0) ↔ 完了行 (row=1)
            _subscriptions.Add(selectionState.P1Row.Subscribe(
                _ => RefreshHighlight(PlayerId.Player1, _leftCardElements)));
            _subscriptions.Add(selectionState.P2Row.Subscribe(
                _ => RefreshHighlight(PlayerId.Player2, _rightCardElements)));
        }

        public void UpdateCountdown()
        {
            if (!_upgradePhase.IsActive) return;
            int seconds = (int)MathF.Ceiling(_upgradePhase.RemainingTime.CurrentValue);
            _view.SetCountdown(seconds);
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
                card.SetData(def);

                bool purchased = _selectionState.IsPurchased(player, i);
                card.SetLocked(!purchased && stats.Coins.CurrentValue < def.Cost);
                card.SetDone(purchased);

                container.Add(card.Root);
                cardElements.Add(card);
            }
        }

        private static void UpdateSelection(List<UpgradeCardElement> cards, int selectedIndex, bool isCardRow)
        {
            for (int i = 0; i < cards.Count; i++)
                cards[i].SetSelected(isCardRow && i == selectedIndex);
        }

        private void RefreshHighlight(PlayerId player, List<UpgradeCardElement> cards)
        {
            int row = _selectionState.GetRow(player);
            int idx = _selectionState.GetIndex(player);
            bool onCardRow = row == 0;
            bool onReroll = onCardRow && idx == 3;
            bool onCard = onCardRow && idx < 3;

            // カードハイライト
            UpdateSelection(cards, onCard ? idx : -1, true);

            // リロールハイライト
            if (player == PlayerId.Player1)
            {
                _view.SetLeftRerollHighlight(onReroll);
                _view.SetLeftDoneHighlight(row == 1);
            }
            else
            {
                _view.SetRightRerollHighlight(onReroll);
                _view.SetRightDoneHighlight(row == 1);
            }
        }

        private void RefreshCardStates(List<UpgradeCardElement> cards, PlayerId player, PlayerStats stats)
        {
            var choices = player == PlayerId.Player1
                ? _upgradePhase.DraftP1.CurrentChoices.CurrentValue
                : _upgradePhase.DraftP2.CurrentChoices.CurrentValue;

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
