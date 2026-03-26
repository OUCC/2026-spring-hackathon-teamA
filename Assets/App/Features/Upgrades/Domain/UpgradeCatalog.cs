using System.Collections.Generic;
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
            // 炎ボム強化 (Common)
            Register(UpgradeId.FireFlightRange,       "遠炎の手甲",     2, false, true,  UpgradeRarity.Common);
            Register(UpgradeId.FireEffectRange,       "猛火の紋章",     3, false, true,  UpgradeRarity.Common);
            Register(UpgradeId.FireDamage,            "灼熱の宝珠",     3, false, true,  UpgradeRarity.Common);
            Register(UpgradeId.FireDuration,          "残炎の灯火",     2, false, true,  UpgradeRarity.Common);
            Register(UpgradeId.FireCooldown,          "速炎の腕輪",     2, false, true,  UpgradeRarity.Common);
            // 炎ボム強化 (Rare)
            Register(UpgradeId.FireWallPenetration,   "侵食の炎",       6, true,  false, UpgradeRarity.Rare);
            Register(UpgradeId.FireBombPenetration,   "貫炎のルーン",   6, true,  false, UpgradeRarity.Rare);

            // ブレークボム強化 (Common)
            Register(UpgradeId.BreakFlightRange,      "剛投の手甲",     3, false, true,  UpgradeRarity.Common);
            Register(UpgradeId.BreakEffectRange,      "震撃の紋章",     4, false, true,  UpgradeRarity.Common);
            Register(UpgradeId.BreakDamage,           "崩壊の宝珠",     4, false, true,  UpgradeRarity.Common);
            Register(UpgradeId.BreakCollapseTime,     "永劫の亀裂",     3, false, true,  UpgradeRarity.Common);
            Register(UpgradeId.BreakCooldown,         "速砕の腕輪",     3, false, true,  UpgradeRarity.Common);
            // ブレークボム強化 (Rare)
            Register(UpgradeId.BreakBombPenetration,  "貫砕のルーン",   7, true,  false, UpgradeRarity.Rare);

            // 汎用強化 (Common)
            Register(UpgradeId.MoveSpeed,             "疾風のブーツ",   2, false, true,  UpgradeRarity.Common);
            Register(UpgradeId.HpRecovery,            "癒しの霊薬",     2, false, false, UpgradeRarity.Common); // 赤スライムドロップ対象外
            // 汎用強化 (Rare) — 一時効果
            Register(UpgradeId.FireShield,            "炎守りのマント", 3, false, false, UpgradeRarity.Rare);
            Register(UpgradeId.Levitation,            "風の羽衣",       4, false, false, UpgradeRarity.Rare);
            // 汎用強化 (Epic) — 永続アビリティ
            Register(UpgradeId.Dash,                  "疾駆の脚甲",    10, true,  false, UpgradeRarity.Epic);
            Register(UpgradeId.DualShot,              "双射の書",      10, true,  false, UpgradeRarity.Epic);
        }

        private void Register(UpgradeId id, string name, int cost, bool onceOnly, bool unlimitedStackable, UpgradeRarity rarity)
        {
            var def = new UpgradeDefinition(id, name, cost, onceOnly, unlimitedStackable, rarity);
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
