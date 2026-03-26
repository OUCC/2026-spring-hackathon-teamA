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
        /// 候補を重み付き抽選する。excludeIds で指定した UpgradeId は候補から除外される
        /// （同一フェーズ内で購入済みの強化を再出現させない）。
        /// レアリティによる出現重み: Common=3, Rare=2, Epic=1
        /// </summary>
        public IReadOnlyList<UpgradeDefinition> Roll(
            PlayerModel player,
            int choiceCount,
            IRandomProvider random,
            HashSet<UpgradeId> excludeIds = null)
        {
            var pool = new List<UpgradeDefinition>();
            var weights = new List<int>();
            int totalWeight = 0;

            foreach (var def in _catalog.GetAll())
            {
                if (!_availabilityRule.IsAvailable(def, player)) continue;
                if (excludeIds != null && excludeIds.Contains(def.Id)) continue;
                pool.Add(def);
                int w = GetRarityWeight(def.Rarity);
                weights.Add(w);
                totalWeight += w;
            }

            var result = new List<UpgradeDefinition>();
            int count = System.Math.Min(choiceCount, pool.Count);

            for (int i = 0; i < count; i++)
            {
                int roll = random.Range(0, totalWeight);
                int cumulative = 0;
                int picked = 0;
                for (int j = 0; j < pool.Count; j++)
                {
                    cumulative += weights[j];
                    if (roll < cumulative)
                    {
                        picked = j;
                        break;
                    }
                }

                result.Add(pool[picked]);
                totalWeight -= weights[picked];
                pool.RemoveAt(picked);
                weights.RemoveAt(picked);
            }

            return result;
        }

        private static int GetRarityWeight(UpgradeRarity rarity)
        {
            return rarity switch
            {
                UpgradeRarity.Common => 3,
                UpgradeRarity.Rare => 2,
                UpgradeRarity.Epic => 1,
                _ => 3,
            };
        }
    }
}
