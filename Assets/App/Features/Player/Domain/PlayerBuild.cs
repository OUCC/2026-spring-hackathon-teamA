using System;
using System.Collections.Generic;
using R3;
using FloorBreaker.Shared.Domain.Primitives;

namespace FloorBreaker.Player.Domain
{
    public sealed class PlayerBuild : IDisposable
    {
        // --- 取得済み強化 ---
        private readonly List<UpgradeId> _acquiredList = new();
        private readonly ReactiveProperty<IReadOnlyList<UpgradeId>> _acquiredUpgrades
            = new(Array.Empty<UpgradeId>());

        public ReadOnlyReactiveProperty<IReadOnlyList<UpgradeId>> AcquiredUpgrades => _acquiredUpgrades;
        // --- 炎ボム ---
        public int FireFlightRange { get; set; }
        public int FireEffectRange { get; set; }
        public int FireDamage { get; set; }
        public float FireCooldown { get; set; }
        public float FireDuration { get; set; }
        public bool FireWallPenetration { get; set; }

        // --- ブレークボム ---
        public int BreakFlightRange { get; set; }
        public int BreakEffectRange { get; set; }
        public int BreakDamage { get; set; }
        public float BreakCooldown { get; set; }
        public float BreakCollapseTime { get; set; }

        // --- CD 下限 ---
        public float FireCooldownMin { get; }
        public float BreakCooldownMin { get; }

        // --- ボム貫通 ---
        public bool HasFireBombPenetration { get; set; }
        public bool HasBreakBombPenetration { get; set; }

        // --- 永続アビリティ ---
        public bool HasDash { get; set; }
        public bool HasDualShot { get; set; }

        // --- 移動速度 ---
        public float MoveSpeedBonus { get; internal set; }

        public PlayerBuild(
            int fireFlightRange, int fireEffectRange, int fireDamage, float fireCooldown,
            float fireDuration, bool fireWallPenetration, float fireCooldownMin,
            int breakFlightRange, int breakEffectRange, int breakDamage, float breakCooldown,
            float breakCollapseTime, float breakCooldownMin)
        {
            FireFlightRange = fireFlightRange;
            FireEffectRange = fireEffectRange;
            FireDamage = fireDamage;
            FireCooldown = fireCooldown;
            FireDuration = fireDuration;
            FireWallPenetration = fireWallPenetration;
            FireCooldownMin = fireCooldownMin;

            BreakFlightRange = breakFlightRange;
            BreakEffectRange = breakEffectRange;
            BreakDamage = breakDamage;
            BreakCooldown = breakCooldown;
            BreakCollapseTime = breakCollapseTime;
            BreakCooldownMin = breakCooldownMin;
        }

        /// <summary>
        /// 強化取得を履歴に記録する。UpgradeApplyService から呼び出す。
        /// </summary>
        public void RecordUpgrade(UpgradeId id)
        {
            _acquiredList.Add(id);
            _acquiredUpgrades.Value = _acquiredList.ToArray();
        }

        /// <summary>
        /// 最後に取得した該当 ID の強化を履歴から除去する。Undo 用。
        /// </summary>
        public void RemoveUpgrade(UpgradeId id)
        {
            for (int i = _acquiredList.Count - 1; i >= 0; i--)
            {
                if (_acquiredList[i] == id)
                {
                    _acquiredList.RemoveAt(i);
                    _acquiredUpgrades.Value = _acquiredList.ToArray();
                    return;
                }
            }
        }

        public void Dispose()
        {
            _acquiredUpgrades.Dispose();
        }
    }
}
