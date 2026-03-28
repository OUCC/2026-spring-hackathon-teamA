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

        public static string GetBadgeClass(UpgradeId id)
        {
            return GetCategory(id) switch
            {
                UpgradeCategory.Fire => "hud__upgrade-badge--fire",
                UpgradeCategory.Break => "hud__upgrade-badge--break",
                _ => "hud__upgrade-badge--general",
            };
        }

        public static string GetShortLabel(UpgradeId id)
        {
            return id switch
            {
                UpgradeId.FireFlightRange => "炎距",
                UpgradeId.FireEffectRange => "炎範",
                UpgradeId.FireDamage => "炎攻",
                UpgradeId.FireDuration => "炎時",
                UpgradeId.FireWallPenetration => "炎貫",
                UpgradeId.FireCooldown => "炎速",
                UpgradeId.FireBombPenetration => "炎飛",
                UpgradeId.BreakFlightRange => "壊距",
                UpgradeId.BreakEffectRange => "壊範",
                UpgradeId.BreakDamage => "壊攻",
                UpgradeId.BreakCollapseTime => "壊時",
                UpgradeId.BreakCooldown => "壊速",
                UpgradeId.BreakBombPenetration => "壊飛",
                UpgradeId.HpRecovery => "回復",
                UpgradeId.MoveSpeed => "移速",
                UpgradeId.Dash => "疾走",
                UpgradeId.DualShot => "双射",
                UpgradeId.FireShield => "炎盾",
                UpgradeId.Levitation => "浮遊",
                _ => "?",
            };
        }

        // Legacy: kept for backward compat if needed
        public static string GetDotClass(UpgradeId id) => GetBadgeClass(id);
    }
}
