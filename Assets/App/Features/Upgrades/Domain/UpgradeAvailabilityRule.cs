using System;
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
                    case UpgradeId.FireFlightDamage:
                        if (build.FireHasFlightDamage) return false;
                        break;
                    case UpgradeId.FireWallPenetration:
                        if (build.FireWallPenetration) return false;
                        break;
                    case UpgradeId.FallFlightDamage:
                        if (build.FallHasFlightDamage) return false;
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
            if (def.Id == UpgradeId.FallCooldown)
            {
                if (build.FallCooldown <= build.FallCooldownMin)
                    return false;
            }

            return true;
        }
    }
}
