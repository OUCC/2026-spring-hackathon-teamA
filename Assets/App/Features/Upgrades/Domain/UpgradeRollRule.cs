using System.Collections.Generic;
using FloorBreaker.Shared.Domain.Primitives;
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

        /// <summary>
        /// 候補を抽選する。excludeIds で指定した UpgradeId は候補から除外される
        /// （同一フェーズ内で購入済みの強化を再出現させない）。
        /// </summary>
        public IReadOnlyList<UpgradeDefinition> Roll(
            PlayerModel player,
            int choiceCount,
            IRandomProvider random,
            HashSet<UpgradeId> excludeIds = null)
        {
            var pool = new List<UpgradeDefinition>();
            foreach (var def in _catalog.GetAll())
            {
                if (!_availabilityRule.IsAvailable(def, player)) continue;
                if (excludeIds != null && excludeIds.Contains(def.Id)) continue;
                pool.Add(def);
            }

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
