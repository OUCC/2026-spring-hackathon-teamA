using System.Collections.Generic;
using FloorBreaker.Shared.Application.Interfaces;
using FloorBreaker.Player.Domain;

namespace FloorBreaker.Upgrades.Domain
{
    public sealed class UpgradeRollRule
    {
        private readonly UpgradeCatalog _catalog;
        private readonly UpgradeAvailabilityRule _availabilityRule;

        public UpgradeRollRule(UpgradeCatalog catalog, UpgradeAvailabilityRule availabilityRule)
        {
            _catalog = catalog;
            _availabilityRule = availabilityRule;
        }

        public IReadOnlyList<UpgradeDefinition> Roll(PlayerModel player, int choiceCount, IRandomProvider random)
        {
            // 候補プール構築
            var pool = new List<UpgradeDefinition>();
            foreach (var def in _catalog.GetAll())
            {
                if (_availabilityRule.IsAvailable(def, player))
                    pool.Add(def);
            }

            // ランダム選択（重複なし）
            var result = new List<UpgradeDefinition>();
            int count = System.Math.Min(choiceCount, pool.Count);

            for (int i = 0; i < count; i++)
            {
                int idx = random.Range(0, pool.Count);
                result.Add(pool[idx]);
                pool.RemoveAt(idx);
            }

            return result;
        }
    }
}
