using System;
using System.Collections.Generic;
using R3;
using FloorBreaker.Shared.Application.Interfaces;
using FloorBreaker.Player.Domain;

namespace FloorBreaker.Upgrades.Domain
{
    public sealed class UpgradeDraftService : IDisposable
    {
        private readonly UpgradeRollRule _rollRule;
        private readonly UpgradeApplyService _applyService;
        private readonly IBalanceParameters _balance;

        private readonly ReactiveProperty<DraftState> _state = new(DraftState.Choosing);
        private readonly ReactiveProperty<IReadOnlyList<UpgradeDefinition>> _currentChoices
            = new(Array.Empty<UpgradeDefinition>());

        public ReadOnlyReactiveProperty<DraftState> State => _state;
        public ReadOnlyReactiveProperty<IReadOnlyList<UpgradeDefinition>> CurrentChoices => _currentChoices;

        public UpgradeDraftService(UpgradeRollRule rollRule, UpgradeApplyService applyService, IBalanceParameters balance)
        {
            _rollRule = rollRule;
            _applyService = applyService;
            _balance = balance;
        }

        public void GenerateChoices(PlayerModel player, IRandomProvider random)
        {
            _state.Value = DraftState.Choosing;
            _currentChoices.Value = _rollRule.Roll(player, _balance.UpgradeChoiceCount, random);
        }

        public bool Reroll(PlayerModel player, IRandomProvider random)
        {
            if (_state.Value != DraftState.Choosing) return false;
            if (!player.Stats.SpendCoins(_balance.RerollCost)) return false;

            _currentChoices.Value = _rollRule.Roll(player, _balance.UpgradeChoiceCount, random);
            return true;
        }

        public bool SelectChoice(int index, PlayerModel player)
        {
            if (_state.Value != DraftState.Choosing) return false;

            var choices = _currentChoices.Value;
            if (index < 0 || index >= choices.Count) return false;

            var chosen = choices[index];
            if (!player.Stats.SpendCoins(chosen.Cost)) return false;

            _applyService.Apply(chosen.Id, player);
            _state.Value = DraftState.Selected;
            return true;
        }

        public void Skip()
        {
            if (_state.Value == DraftState.Choosing)
                _state.Value = DraftState.Skipped;
        }

        public void TimeOut()
        {
            if (_state.Value == DraftState.Choosing)
                _state.Value = DraftState.TimedOut;
        }

        public void Reset()
        {
            _state.Value = DraftState.Choosing;
            _currentChoices.Value = Array.Empty<UpgradeDefinition>();
        }

        public void Dispose()
        {
            _state.Dispose();
            _currentChoices.Dispose();
        }
    }
}
