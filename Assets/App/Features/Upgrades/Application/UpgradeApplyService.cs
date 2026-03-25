using System;
using FloorBreaker.Shared.Domain.Primitives;
using FloorBreaker.Shared.Application.Interfaces;
using FloorBreaker.Player.Domain;

namespace FloorBreaker.Upgrades.Domain
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
                case UpgradeId.MoveSpeed:
                    player.Stats.MoveSpeed = MathF.Min(
                        player.Stats.MaxMoveSpeed,
                        player.Stats.MoveSpeed + _balance.MovementSpeedIncrement);
                    break;

                case UpgradeId.HpRecovery:
                    player.Stats.Heal(_balance.HpRecoveryAmount);
                    break;

                default:
                    // ボム強化は PlayerBuild に委譲
                    player.Build.ApplyUpgrade(id);
                    break;
            }
        }
    }
}
