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
        public int FireFlightRange { get; private set; }
        public int FireEffectRange { get; private set; }
        public int FireDamage { get; private set; }
        public float FireCooldown { get; private set; }
        public float FireDuration { get; private set; }
        public bool FireWallPenetration { get; private set; }

        // --- ブレークボム ---
        public int BreakFlightRange { get; private set; }
        public int BreakEffectRange { get; private set; }
        public int BreakDamage { get; private set; }
        public float BreakCooldown { get; private set; }
        public float BreakCollapseTime { get; private set; }

        // --- CD 下限 ---
        public float FireCooldownMin { get; }
        public float BreakCooldownMin { get; }

        // --- ボム貫通 ---
        public bool HasFireBombPenetration { get; private set; }
        public bool HasBreakBombPenetration { get; private set; }

        // --- 永続アビリティ ---
        public bool HasDash { get; private set; }
        public bool HasDualShot { get; private set; }

        // --- 移動速度 ---
        public float MoveSpeedBonus { get; private set; }

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

        public void ApplyUpgrade(UpgradeId id)
        {
            switch (id)
            {
                case UpgradeId.FireFlightRange:
                    FireFlightRange += 2;
                    break;
                case UpgradeId.FireEffectRange:
                    FireEffectRange++;
                    break;
                case UpgradeId.FireDamage:
                    FireDamage++;
                    break;
                case UpgradeId.FireDuration:
                    FireDuration += 2f;
                    break;
                case UpgradeId.FireWallPenetration:
                    FireWallPenetration = true;
                    break;
                case UpgradeId.FireCooldown:
                    FireCooldown = MathF.Max(FireCooldownMin, FireCooldown - 0.3f);
                    break;
                case UpgradeId.BreakFlightRange:
                    BreakFlightRange += 2;
                    break;
                case UpgradeId.BreakEffectRange:
                    BreakEffectRange++;
                    break;
                case UpgradeId.BreakDamage:
                    BreakDamage++;
                    break;
                case UpgradeId.BreakCollapseTime:
                    BreakCollapseTime += 2f;
                    break;
                case UpgradeId.BreakCooldown:
                    BreakCooldown = MathF.Max(BreakCooldownMin, BreakCooldown - 0.5f);
                    break;
                case UpgradeId.FireBombPenetration:
                    HasFireBombPenetration = true;
                    break;
                case UpgradeId.BreakBombPenetration:
                    HasBreakBombPenetration = true;
                    break;
                case UpgradeId.Dash:
                    HasDash = true;
                    break;
                case UpgradeId.DualShot:
                    HasDualShot = true;
                    break;
                // MoveSpeed, HpRecovery, FireShield, Levitation は PlayerStats/UpgradeApplyService 側で適用
            }

            RecordUpgrade(id);
        }

        /// <summary>
        /// 強化取得を履歴に記録する。ボム強化は ApplyUpgrade 内で自動呼び出し。
        /// MoveSpeed / HpRecovery は UpgradeApplyService から明示的に呼ぶ。
        /// </summary>
        public void RecordUpgrade(UpgradeId id)
        {
            _acquiredList.Add(id);
            _acquiredUpgrades.Value = _acquiredList.ToArray();
        }

        public void Dispose()
        {
            _acquiredUpgrades.Dispose();
        }
    }
}
