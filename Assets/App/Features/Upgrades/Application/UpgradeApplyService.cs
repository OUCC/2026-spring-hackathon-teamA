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

        /// <summary>
        /// Apply 前に呼び出し、undo 用のクロージャを返す。
        /// クロージャは Apply 前の状態をキャプチャし、呼び出すと元に戻す。
        /// </summary>
        public Action<PlayerModel> CaptureUndo(UpgradeId id, PlayerModel player)
        {
            switch (id)
            {
                case UpgradeId.FireFlightRange:
                    { var prev = player.Build.FireFlightRange; return p => p.Build.FireFlightRange = prev; }
                case UpgradeId.FireEffectRange:
                    { var prev = player.Build.FireEffectRange; return p => p.Build.FireEffectRange = prev; }
                case UpgradeId.FireDamage:
                    { var prev = player.Build.FireDamage; return p => p.Build.FireDamage = prev; }
                case UpgradeId.FireDuration:
                    { var prev = player.Build.FireDuration; return p => p.Build.FireDuration = prev; }
                case UpgradeId.FireCooldown:
                    { var prev = player.Build.FireCooldown; return p => p.Build.FireCooldown = prev; }
                case UpgradeId.FireWallPenetration:
                    { var prev = player.Build.FireWallPenetration; return p => p.Build.FireWallPenetration = prev; }
                case UpgradeId.FireBombPenetration:
                    { var prev = player.Build.HasFireBombPenetration; return p => p.Build.HasFireBombPenetration = prev; }
                case UpgradeId.BreakFlightRange:
                    { var prev = player.Build.BreakFlightRange; return p => p.Build.BreakFlightRange = prev; }
                case UpgradeId.BreakEffectRange:
                    { var prev = player.Build.BreakEffectRange; return p => p.Build.BreakEffectRange = prev; }
                case UpgradeId.BreakDamage:
                    { var prev = player.Build.BreakDamage; return p => p.Build.BreakDamage = prev; }
                case UpgradeId.BreakCollapseTime:
                    { var prev = player.Build.BreakCollapseTime; return p => p.Build.BreakCollapseTime = prev; }
                case UpgradeId.BreakCooldown:
                    { var prev = player.Build.BreakCooldown; return p => p.Build.BreakCooldown = prev; }
                case UpgradeId.BreakBombPenetration:
                    { var prev = player.Build.HasBreakBombPenetration; return p => p.Build.HasBreakBombPenetration = prev; }
                case UpgradeId.MoveSpeed:
                    { var prev = player.Stats.MoveSpeed; return p => p.Stats.MoveSpeed = prev; }
                case UpgradeId.HpRecovery:
                    { var prevHp = player.Stats.CurrentHpValue; return p => p.Stats.SetHp(prevHp); }
                case UpgradeId.FireShield:
                    return p => p.Stats.DeactivateFireShield();
                case UpgradeId.Levitation:
                    return p => p.Stats.DeactivateLevitation();
                case UpgradeId.Dash:
                    { var prev = player.Build.HasDash; return p => p.Build.HasDash = prev; }
                case UpgradeId.DualShot:
                    { var prev = player.Build.HasDualShot; return p => p.Build.HasDualShot = prev; }
                default:
                    return _ => { };
            }
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
