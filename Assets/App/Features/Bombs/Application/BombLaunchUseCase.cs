using System;
using System.Collections.Generic;
using FloorBreaker.Shared.Domain.Grid;
using FloorBreaker.Shared.Domain.Primitives;
using FloorBreaker.Shared.Application.Interfaces;
using FloorBreaker.Stage.Domain;
using FloorBreaker.Player.Domain;
using FloorBreaker.Bombs.Domain;

namespace FloorBreaker.Bombs.Application
{
    public sealed class BombLaunchUseCase
    {
        private readonly BombLandingResolver _landingResolver;
        private readonly FallBombResolver _fallResolver;
        private readonly FireBombResolver _fireResolver;
        private readonly StageModel _stage;
        private readonly float _fallBombRecoveryDuration;
        private readonly BombEffectSpreadService _spreadService;
        private readonly float _fireSpreadInterval;
        private readonly float _fallSpreadInterval;

        public BombLaunchUseCase(
            BombLandingResolver landingResolver,
            FallBombResolver fallResolver,
            FireBombResolver fireResolver,
            StageModel stage,
            IBalanceParameters balance,
            BombEffectSpreadService spreadService)
        {
            _landingResolver = landingResolver;
            _fallResolver = fallResolver;
            _fireResolver = fireResolver;
            _stage = stage;
            _fallBombRecoveryDuration = balance.FallBombRecoveryDuration;
            _spreadService = spreadService;
            _fireSpreadInterval = balance.FireBombSpreadInterval;
            _fallSpreadInterval = balance.FallBombSpreadInterval;
        }

        public BombSpec CreateFallBombSpec(PlayerBuild build)
        {
            return new BombSpec(
                BombType.Fall,
                build.FallFlightRange,
                build.FallEffectRange,
                build.FallDamage,
                build.FallCooldown,
                build.FallHasFlightDamage,
                true, // 滑落ボムは常に壁貫通
                0f,
                build.FallCollapseTime,
                _fallBombRecoveryDuration);
        }

        public BombSpec CreateFireBombSpec(PlayerBuild build)
        {
            return new BombSpec(
                BombType.Fire,
                build.FireFlightRange,
                build.FireEffectRange,
                build.FireDamage,
                build.FireCooldown,
                build.FireHasFlightDamage,
                build.FireWallPenetration,
                build.FireDuration,
                0f,
                0f);
        }

        /// <summary>
        /// 着弾位置を解決する。
        /// </summary>
        public GridPos ResolveLanding(BombFlightCommand cmd, int actualFlightDistance, Func<GridPos, bool> isEntityAt)
        {
            return _landingResolver.Resolve(cmd, actualFlightDistance, isEntityAt);
        }

        /// <summary>
        /// ボム着弾後の効果を適用する。
        /// </summary>
        public void ExecuteLanding(
            BombFlightCommand cmd,
            GridPos landingPos,
            IReadOnlyList<PlayerModel> players,
            Func<GridPos, bool> isEntityAt)
        {
            // ボム所有者の PlayerModel を特定
            PlayerModel owner = null;
            foreach (var p in players)
            {
                if (p.Id == cmd.Owner) { owner = p; break; }
            }

            switch (cmd.Spec.Type)
            {
                case BombType.Fall:
                    ExecuteFallBomb(landingPos, cmd.Spec, players, owner);
                    break;
                case BombType.Fire:
                    ExecuteFireBomb(landingPos, cmd.Spec, players, owner);
                    break;
            }
        }

        private void ExecuteFallBomb(GridPos landingPos, BombSpec spec, IReadOnlyList<PlayerModel> players, PlayerModel owner)
        {
            var result = _fallResolver.Resolve(landingPos, spec, _stage);
            _spreadService.EnqueueFallBomb(result, landingPos, players, owner, _fallSpreadInterval);
        }

        private void ExecuteFireBomb(GridPos landingPos, BombSpec spec, IReadOnlyList<PlayerModel> players, PlayerModel owner)
        {
            var result = _fireResolver.Resolve(landingPos, spec, _stage);
            _spreadService.EnqueueFireBomb(result, landingPos, players, owner, _fireSpreadInterval);
        }
    }
}
