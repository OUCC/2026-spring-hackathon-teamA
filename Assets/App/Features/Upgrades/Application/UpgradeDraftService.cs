using System;
using System.Collections.Generic;
using R3;
using FloorBreaker.Shared.Domain.Primitives;
using FloorBreaker.Shared.Application.Interfaces;
using FloorBreaker.Upgrades.Domain;
using FloorBreaker.Player.Domain;

namespace FloorBreaker.Upgrades.Application
{
    /// <summary>購入記録。Undo 時に使用する。</summary>
    public readonly struct PurchaseRecord
    {
        public readonly UpgradeId Id;
        public readonly int CardIndex;
        public readonly int CoinCost;
        public readonly Action<PlayerModel> UndoAction;

        public PurchaseRecord(UpgradeId id, int cardIndex, int coinCost, Action<PlayerModel> undoAction)
        {
            Id = id;
            CardIndex = cardIndex;
            CoinCost = coinCost;
            UndoAction = undoAction;
        }
    }

    public sealed class UpgradeDraftService : IDisposable
    {
        private readonly UpgradeRollRule _rollRule;
        private readonly UpgradeApplyService _applyService;
        private readonly IBalanceParameters _balance;

        private readonly ReactiveProperty<DraftState> _state = new(DraftState.Choosing);
        private readonly ReactiveProperty<IReadOnlyList<UpgradeDefinition>> _currentChoices
            = new(Array.Empty<UpgradeDefinition>());

        // 同一フェーズ内で購入済みの UpgradeId (リロール時の除外用)
        private readonly HashSet<UpgradeId> _purchasedThisPhase = new();

        // Undo 用の購入履歴スタック
        private readonly Stack<PurchaseRecord> _purchaseHistory = new();

        public ReadOnlyReactiveProperty<DraftState> State => _state;
        public ReadOnlyReactiveProperty<IReadOnlyList<UpgradeDefinition>> CurrentChoices => _currentChoices;
        public bool CanUndo => _purchaseHistory.Count > 0;

        public UpgradeDraftService(UpgradeRollRule rollRule, UpgradeApplyService applyService, IBalanceParameters balance)
        {
            _rollRule = rollRule;
            _applyService = applyService;
            _balance = balance;
        }

        public void GenerateChoices(PlayerModel player, IRandomProvider random)
        {
            _state.Value = DraftState.Choosing;
            _purchasedThisPhase.Clear();
            _purchaseHistory.Clear();
            _currentChoices.Value = _rollRule.Roll(player, _balance.UpgradeChoiceCount, random, _purchasedThisPhase);
        }

        public bool Reroll(PlayerModel player, IRandomProvider random)
        {
            if (_state.Value != DraftState.Choosing) return false;
            if (!player.Stats.SpendCoins(_balance.RerollCost)) return false;

            _currentChoices.Value = _rollRule.Roll(player, _balance.UpgradeChoiceCount, random, _purchasedThisPhase);
            return true;
        }

        /// <summary>
        /// 選択した強化を購入・適用する。複数回呼び出し可能（Choosing を維持）。
        /// 終了は Skip() で明示的に行う。
        /// </summary>
        public bool SelectChoice(int index, PlayerModel player)
        {
            if (_state.Value != DraftState.Choosing) return false;

            var choices = _currentChoices.Value;
            if (index < 0 || index >= choices.Count) return false;

            var chosen = choices[index];
            if (!player.Stats.SpendCoins(chosen.Cost)) return false;

            // Apply 前の状態をキャプチャして undo action を作成
            var undoAction = _applyService.CaptureUndo(chosen.Id, player);
            _applyService.Apply(chosen.Id, player);
            _purchasedThisPhase.Add(chosen.Id);
            _purchaseHistory.Push(new PurchaseRecord(chosen.Id, index, chosen.Cost, undoAction));
            // Choosing を維持 — 複数選択可能
            return true;
        }

        /// <summary>
        /// 最後の購入を取り消す。コインを返却し、強化を元に戻す。
        /// </summary>
        public PurchaseRecord? UndoLastPurchase(PlayerModel player)
        {
            if (_state.Value != DraftState.Choosing) return null;
            if (_purchaseHistory.Count == 0) return null;

            var record = _purchaseHistory.Pop();
            record.UndoAction?.Invoke(player);
            player.Stats.AddCoins(record.CoinCost);
            _purchasedThisPhase.Remove(record.Id);
            player.Build.RemoveUpgrade(record.Id);
            return record;
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
