using System.Collections.Generic;
using System.Linq;
using FloorBreaker.Shared.Domain.Primitives;

namespace FloorBreaker.Upgrades.Domain
{
    public sealed class UpgradeCatalog
    {
        private readonly Dictionary<UpgradeId, UpgradeDefinition> _definitions = new();
        private readonly List<UpgradeDefinition> _all = new();
        private readonly List<UpgradeDefinition> _unlimitedStackables = new();

        public UpgradeCatalog()
        {
            // 炎ボム強化
            Register(UpgradeId.FireFlightRange,    "炎ボム飛距離増強",       2, false, true);
            Register(UpgradeId.FireEffectRange,    "炎ボムフライ",           3, false, true);
            Register(UpgradeId.FireDamage,         "炎ボム威力増強",         3, false, true);
            Register(UpgradeId.FireFlightDamage,   "炎ボム飛行時ダメージ",   5, true,  false);
            Register(UpgradeId.FireDuration,       "炎ボム延焼時間増加",     2, false, true);
            Register(UpgradeId.FireWallPenetration,"延焼の壁貫通",           6, true,  false);
            Register(UpgradeId.FireCooldown,       "炎ボムクールダウン短縮", 2, false, true);

            // 滑落ボム強化
            Register(UpgradeId.FallFlightRange,  "滑落ボム飛距離増強",       3, false, true);
            Register(UpgradeId.FallEffectRange,  "滑落ボムフライ",           4, false, true);
            Register(UpgradeId.FallDamage,       "滑落ボム威力増強",         4, false, true);
            Register(UpgradeId.FallFlightDamage, "滑落ボム飛行時ダメージ",   6, true,  false);
            Register(UpgradeId.FallCollapseTime, "滑落ボム崩落時間増加",     3, false, true);
            Register(UpgradeId.FallCooldown,     "滑落ボムクールダウン短縮", 3, false, true);

            // 汎用強化
            Register(UpgradeId.MoveSpeed,   "移動速度上昇", 2, false, true);
            Register(UpgradeId.HpRecovery,  "体力回復",     2, false, false); // 無制限だが赤スライムドロップ対象外
        }

        private void Register(UpgradeId id, string name, int cost, bool onceOnly, bool unlimitedStackable)
        {
            var def = new UpgradeDefinition(id, name, cost, onceOnly, unlimitedStackable);
            _definitions[id] = def;
            _all.Add(def);
            if (unlimitedStackable)
                _unlimitedStackables.Add(def);
        }

        public UpgradeDefinition GetById(UpgradeId id) => _definitions[id];

        public IReadOnlyList<UpgradeDefinition> GetAll() => _all;

        /// <summary>
        /// 赤色スライムドロップ対象: 無制限取得可能な強化一覧。
        /// HP回復は条件付きなので含まない。
        /// </summary>
        public IReadOnlyList<UpgradeDefinition> GetUnlimitedStackables() => _unlimitedStackables;
    }
}
