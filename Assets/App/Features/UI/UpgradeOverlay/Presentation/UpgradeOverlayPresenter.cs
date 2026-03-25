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
        private readonly VisualTreeAsset _cardTemplate;

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
            _cardTemplate = cardTemplate;

            _subscriptions.Add(clock.CurrentPhase.Subscribe(phase =>
            {
                if (phase == GamePhase.UpgradePhase)
                    _view.Show();
                else
                    _view.Hide();
            }));

            _subscriptions.Add(upgradePhase.DraftP1.CurrentChoices.Subscribe(
                choices => PopulateCards(_view.LeftCards, choices, _leftCardElements, p1Stats)));

            _subscriptions.Add(upgradePhase.DraftP2.CurrentChoices.Subscribe(
                choices => PopulateCards(_view.RightCards, choices, _rightCardElements, p2Stats)));

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

            _subscriptions.Add(selectionState.P1Index.Subscribe(
                idx => UpdateSelection(_leftCardElements, idx)));

            _subscriptions.Add(selectionState.P2Index.Subscribe(
                idx => UpdateSelection(_rightCardElements, idx)));
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
            PlayerStats stats)
        {
            container.Clear();
            cardElements.Clear();

            foreach (var def in choices)
            {
                var card = new UpgradeCardElement(_cardTemplate);
                card.SetData(def);
                card.SetLocked(stats.Coins.CurrentValue < def.Cost);
                container.Add(card.Root);
                cardElements.Add(card);
            }
        }

        private static void UpdateSelection(List<UpgradeCardElement> cards, int selectedIndex)
        {
            for (int i = 0; i < cards.Count; i++)
                cards[i].SetSelected(i == selectedIndex);
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
                DraftState.Skipped => "スキップ",
                DraftState.TimedOut => "タイムアウト",
                _ => "",
            };
        }
    }
}
