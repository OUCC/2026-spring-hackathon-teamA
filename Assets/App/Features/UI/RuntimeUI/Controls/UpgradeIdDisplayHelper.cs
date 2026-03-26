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
            Break,
            General,
        }

        public static UpgradeCategory GetCategory(UpgradeId id)
        {
            switch (id)
            {
                case UpgradeId.FireFlightRange:
                case UpgradeId.FireEffectRange:
                case UpgradeId.FireDamage:
                case UpgradeId.FireDuration:
                case UpgradeId.FireWallPenetration:
                case UpgradeId.FireCooldown:
                case UpgradeId.FireBombPenetration:
                    return UpgradeCategory.Fire;

                case UpgradeId.BreakFlightRange:
                case UpgradeId.BreakEffectRange:
                case UpgradeId.BreakDamage:
                case UpgradeId.BreakCollapseTime:
                case UpgradeId.BreakCooldown:
                case UpgradeId.BreakBombPenetration:
                    return UpgradeCategory.Break;

                default:
                    return UpgradeCategory.General;
            }
        }

        public static string GetBandClass(UpgradeId id)
        {
            return GetCategory(id) switch
            {
                UpgradeCategory.Fire => "card__band--fire",
                UpgradeCategory.Break => "card__band--break",
                _ => "card__band--general",
            };
        }

        public static string GetDotClass(UpgradeId id)
        {
            return GetCategory(id) switch
            {
                UpgradeCategory.Fire => "hud__upgrade-dot--fire",
                UpgradeCategory.Break => "hud__upgrade-dot--break",
                _ => "hud__upgrade-dot--general",
            };
        }
    }
}
