using FloorBreaker.Shared.Domain.Primitives;

namespace FloorBreaker.UI.RuntimeUI.Controls
{
    /// <summary>
    /// UpgradeId からカテゴリ USS クラスや短縮ラベルを導出するヘルパー。
    /// </summary>
    public static class UpgradeIdDisplayHelper
    {
        public enum UpgradeCategory
        {
            Fire,
            Fall,
            General,
        }

        public static UpgradeCategory GetCategory(UpgradeId id)
        {
            switch (id)
            {
                case UpgradeId.FireFlightRange:
                case UpgradeId.FireEffectRange:
                case UpgradeId.FireDamage:
                case UpgradeId.FireFlightDamage:
                case UpgradeId.FireDuration:
                case UpgradeId.FireWallPenetration:
                case UpgradeId.FireCooldown:
                    return UpgradeCategory.Fire;

                case UpgradeId.FallFlightRange:
                case UpgradeId.FallEffectRange:
                case UpgradeId.FallDamage:
                case UpgradeId.FallFlightDamage:
                case UpgradeId.FallCollapseTime:
                case UpgradeId.FallCooldown:
                    return UpgradeCategory.Fall;

                default:
                    return UpgradeCategory.General;
            }
        }

        public static string GetBandClass(UpgradeId id)
        {
            return GetCategory(id) switch
            {
                UpgradeCategory.Fire => "card__band--fire",
                UpgradeCategory.Fall => "card__band--fall",
                _ => "card__band--general",
            };
        }

        public static string GetDotClass(UpgradeId id)
        {
            return GetCategory(id) switch
            {
                UpgradeCategory.Fire => "hud__upgrade-dot--fire",
                UpgradeCategory.Fall => "hud__upgrade-dot--fall",
                _ => "hud__upgrade-dot--general",
            };
        }
    }
}
