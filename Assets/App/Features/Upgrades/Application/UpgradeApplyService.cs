using System;
using FloorBreaker.Shared.Domain.Primitives;
using FloorBreaker.Shared.Application.Interfaces;
using FloorBreaker.Player.Domain;

namespace FloorBreaker.Upgrades.Application
{
    public sealed class UpgradeApplyService
    {
        private readonly IBalanceParameters _balance;

        public UpgradeApplyService(IBalanceParameters balance)
        {
            _balance = balance;
        }

        public void Apply(UpgradeId id, PlayerModel player)
        {
            switch (id)
            {
                // --- 炎ボム強化 ---
                case UpgradeId.FireFlightRange:
                    player.Build.FireFlightRange += _balance.FireFlightRangeIncrement;
                    break;
                case UpgradeId.FireEffectRange:
                    player.Build.FireEffectRange += _balance.FireEffectRangeIncrement;
                    break;
                case UpgradeId.FireDamage:
                    player.Build.FireDamage += _balance.FireDamageIncrement;
                    break;
                case UpgradeId.FireDuration:
                    player.Build.FireDuration += _balance.FireDurationIncrement;
                    break;
                case UpgradeId.FireCooldown:
                    player.Build.FireCooldown = MathF.Max(
                        player.Build.FireCooldownMin,
                        player.Build.FireCooldown - _balance.FireCooldownReduction);
                    break;
                case UpgradeId.FireWallPenetration:
                    player.Build.FireWallPenetration = true;
                    break;
                case UpgradeId.FireBombPenetration:
                    player.Build.HasFireBombPenetration = true;
                    break;

                // --- ブレークボム強化 ---
                case UpgradeId.BreakFlightRange:
                    player.Build.BreakFlightRange += _balance.BreakFlightRangeIncrement;
                    break;
                case UpgradeId.BreakEffectRange:
                    player.Build.BreakEffectRange += _balance.BreakEffectRangeIncrement;
                    break;
                case UpgradeId.BreakDamage:
                    player.Build.BreakDamage += _balance.BreakDamageIncrement;
                    break;
                case UpgradeId.BreakCollapseTime:
                    player.Build.BreakCollapseTime += _balance.BreakCollapseTimeIncrement;
                    break;
                case UpgradeId.BreakCooldown:
                    player.Build.BreakCooldown = MathF.Max(
                        player.Build.BreakCooldownMin,
                        player.Build.BreakCooldown - _balance.BreakCooldownReduction);
                    break;
                case UpgradeId.BreakBombPenetration:
                    player.Build.HasBreakBombPenetration = true;
                    break;

                // --- 汎用強化 ---
                case UpgradeId.MoveSpeed:
                    player.Stats.MoveSpeed = MathF.Min(
                        player.Stats.MaxMoveSpeed,
                        player.Stats.MoveSpeed + _balance.MovementSpeedIncrement);
                    break;
                case UpgradeId.HpRecovery:
                    player.Stats.Heal(_balance.HpRecoveryAmount);
                    break;
                case UpgradeId.FireShield:
                    player.Stats.ActivateFireShield();
                    break;
                case UpgradeId.Levitation:
                    player.Stats.ActivateLevitation();
                    break;
                case UpgradeId.Dash:
                    player.Build.HasDash = true;
                    break;
                case UpgradeId.DualShot:
                    player.Build.HasDualShot = true;
                    break;
            }

            player.Build.RecordUpgrade(id);
        }
    }
}
