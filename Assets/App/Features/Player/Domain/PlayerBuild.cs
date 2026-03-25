using System;
using FloorBreaker.Shared.Domain.Primitives;

namespace FloorBreaker.Player.Domain
{
    public sealed class PlayerBuild
    {
        // --- 炎ボム ---
        public int FireFlightRange { get; private set; }
        public int FireEffectRange { get; private set; }
        public int FireDamage { get; private set; }
        public float FireCooldown { get; private set; }
        public bool FireHasFlightDamage { get; private set; }
        public float FireDuration { get; private set; }
        public bool FireWallPenetration { get; private set; }

        // --- 滑落ボム ---
        public int FallFlightRange { get; private set; }
        public int FallEffectRange { get; private set; }
        public int FallDamage { get; private set; }
        public float FallCooldown { get; private set; }
        public bool FallHasFlightDamage { get; private set; }
        public float FallCollapseTime { get; private set; }

        // --- CD 下限 ---
        public float FireCooldownMin { get; }
        public float FallCooldownMin { get; }

        // --- 移動速度 ---
        public float MoveSpeedBonus { get; private set; }

        public PlayerBuild(
            int fireFlightRange, int fireEffectRange, int fireDamage, float fireCooldown,
            float fireDuration, bool fireWallPenetration, float fireCooldownMin,
            int fallFlightRange, int fallEffectRange, int fallDamage, float fallCooldown,
            float fallCollapseTime, float fallCooldownMin)
        {
            FireFlightRange = fireFlightRange;
            FireEffectRange = fireEffectRange;
            FireDamage = fireDamage;
            FireCooldown = fireCooldown;
            FireDuration = fireDuration;
            FireWallPenetration = fireWallPenetration;
            FireCooldownMin = fireCooldownMin;

            FallFlightRange = fallFlightRange;
            FallEffectRange = fallEffectRange;
            FallDamage = fallDamage;
            FallCooldown = fallCooldown;
            FallCollapseTime = fallCollapseTime;
            FallCooldownMin = fallCooldownMin;
        }

        public void ApplyUpgrade(UpgradeId id)
        {
            switch (id)
            {
                case UpgradeId.FireFlightRange:
                    FireFlightRange++;
                    break;
                case UpgradeId.FireEffectRange:
                    FireEffectRange++;
                    break;
                case UpgradeId.FireDamage:
                    FireDamage++;
                    break;
                case UpgradeId.FireFlightDamage:
                    FireHasFlightDamage = true;
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
                case UpgradeId.FallFlightRange:
                    FallFlightRange++;
                    break;
                case UpgradeId.FallEffectRange:
                    FallEffectRange++;
                    break;
                case UpgradeId.FallDamage:
                    FallDamage++;
                    break;
                case UpgradeId.FallFlightDamage:
                    FallHasFlightDamage = true;
                    break;
                case UpgradeId.FallCollapseTime:
                    FallCollapseTime += 2f;
                    break;
                case UpgradeId.FallCooldown:
                    FallCooldown = MathF.Max(FallCooldownMin, FallCooldown - 0.5f);
                    break;
                // MoveSpeed と HpRecovery は PlayerStats 側で適用
            }
        }
    }
}
