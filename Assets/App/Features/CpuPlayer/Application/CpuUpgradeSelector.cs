using System.Collections.Generic;
using FloorBreaker.Player.Domain;
using FloorBreaker.Upgrades.Domain;
using FloorBreaker.Upgrades.Application;

namespace FloorBreaker.CpuPlayer.Application
{
    /// <summary>
    /// 強化フェーズでの CPU 選択ロジック。
    /// 購入可能な強化を評価し、コストが高い(=強力な)ものから購入する。
    /// </summary>
    public sealed class CpuUpgradeSelector
    {
        private const float InitialDelay = 1.5f;
        private const float PurchaseInterval = 0.6f;

        private readonly UpgradeDraftService _draft;
        private readonly PlayerModel _cpu;

        private float _delayTimer;
        private float _purchaseTimer;
        private bool _initialDelayDone;
        private bool _done;

        public CpuUpgradeSelector(UpgradeDraftService draft, PlayerModel cpu)
        {
            _draft = draft;
            _cpu = cpu;
        }

        public void Reset()
        {
            _delayTimer = 0f;
            _purchaseTimer = 0f;
            _initialDelayDone = false;
            _done = false;
        }

        public void Tick(float deltaTime)
        {
            if (_done) return;
            if (_draft.State.CurrentValue != DraftState.Choosing) return;

            // 初期ディレイ (自然さのため)
            if (!_initialDelayDone)
            {
                _delayTimer += deltaTime;
                if (_delayTimer < InitialDelay) return;
                _initialDelayDone = true;
            }

            // 購入間隔
            _purchaseTimer += deltaTime;
            if (_purchaseTimer < PurchaseInterval) return;
            _purchaseTimer = 0f;

            // 購入可能なカードを探す
            if (!TryPurchaseBest())
            {
                // 買えるものがない → Skip
                _draft.Skip();
                _done = true;
            }
        }

        private bool TryPurchaseBest()
        {
            var choices = _draft.CurrentChoices.CurrentValue;
            if (choices == null || choices.Count == 0) return false;

            int coins = _cpu.Stats.Coins.CurrentValue;

            // 購入可能なカードの中から最もコストの高いものを選ぶ
            int bestIndex = -1;
            int bestCost = -1;

            for (int i = 0; i < choices.Count; i++)
            {
                var def = choices[i];
                if (def.Cost <= coins && def.Cost > bestCost)
                {
                    bestCost = def.Cost;
                    bestIndex = i;
                }
            }

            if (bestIndex < 0) return false;

            return _draft.SelectChoice(bestIndex, _cpu);
        }
    }
}
