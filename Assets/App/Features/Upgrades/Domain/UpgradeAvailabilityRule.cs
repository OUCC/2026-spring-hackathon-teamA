using FloorBreaker.Shared.Domain.Primitives;
using FloorBreaker.Shared.Application.Interfaces;
using FloorBreaker.Player.Domain;

namespace FloorBreaker.Upgrades.Domain
{
    public sealed class UpgradeAvailabilityRule
    {
        private readonly IBalanceParameters _balance;

        public UpgradeAvailabilityRule(IBalanceParameters balance)
        {
            _balance = balance;
        }

        public bool IsAvailable(UpgradeDefinition def, PlayerModel player)
        {
            var build = player.Build;
            var stats = player.Stats;

            // 1回限り強化: 取得済みなら除外
            if (def.IsOnceOnly)
            {
                switch (def.Id)
                {
                    case UpgradeId.FireWallPenetration:
                        if (build.FireWallPenetration) return false;
                        break;
                    case UpgradeId.FireBombPenetration:
                        if (build.HasFireBombPenetration) return false;
                        break;
                    case UpgradeId.BreakBombPenetration:
                        if (build.HasBreakBombPenetration) return false;
                        break;
                    case UpgradeId.Dash:
                        if (build.HasDash) return false;
                        break;
                    case UpgradeId.DualShot:
                        if (build.HasDualShot) return false;
                        break;
                }
            }

            // HP回復: HP ≦ threshold のときのみ
            if (def.Id == UpgradeId.HpRecovery)
            {
                if (stats.CurrentHp.CurrentValue > _balance.HpRecoveryThreshold)
                    return false;
            }

            // 移動速度: 上限到達で除外
            if (def.Id == UpgradeId.MoveSpeed)
            {
                if (stats.MoveSpeed >= stats.MaxMoveSpeed)
                    return false;
            }

            // CD短縮: 下限到達で除外
            if (def.Id == UpgradeId.FireCooldown)
            {
                if (build.FireCooldown <= build.FireCooldownMin)
                    return false;
            }
            if (def.Id == UpgradeId.BreakCooldown)
            {
                if (build.BreakCooldown <= build.BreakCooldownMin)
                    return false;
            }

            // 一時効果: 効果アクティブ中は出現しない
            if (def.Id == UpgradeId.FireShield)
            {
                if (stats.FireShieldActive.CurrentValue) return false;
            }
            if (def.Id == UpgradeId.Levitation)
            {
                if (stats.LevitationActive.CurrentValue) return false;
            }

            return true;
        }
    }
}
