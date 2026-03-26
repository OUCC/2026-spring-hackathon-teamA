using FloorBreaker.Shared.Domain.Primitives;

namespace FloorBreaker.Upgrades.Domain
{
    public readonly struct UpgradeDefinition
    {
        public readonly UpgradeId Id;
        public readonly string DisplayName;
        public readonly int Cost;
        public readonly bool IsOnceOnly;
        public readonly bool IsUnlimitedStackable;
        public readonly UpgradeRarity Rarity;

        public UpgradeDefinition(UpgradeId id, string displayName, int cost,
            bool isOnceOnly, bool isUnlimitedStackable, UpgradeRarity rarity = UpgradeRarity.Common)
        {
            Id = id;
            DisplayName = displayName;
            Cost = cost;
            IsOnceOnly = isOnceOnly;
            IsUnlimitedStackable = isUnlimitedStackable;
            Rarity = rarity;
        }
    }
}
