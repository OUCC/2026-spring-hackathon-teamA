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
        private readonly BreakBombResolver _breakResolver;
        private readonly FireBombResolver _fireResolver;
        private readonly StageModel _stage;
        private readonly float _breakBombRecoveryDuration;
        private readonly int _bombMinFlightDistance;
        private readonly BombEffectSpreadService _spreadService;
        private readonly float _fireSpreadInterval;
        private readonly float _breakSpreadInterval;

        public BombLaunchUseCase(
            BombLandingResolver landingResolver,
            BreakBombResolver breakResolver,
            FireBombResolver fireResolver,
            StageModel stage,
            IBalanceParameters balance,
            BombEffectSpreadService spreadService)
        {
            _landingResolver = landingResolver;
            _breakResolver = breakResolver;
            _fireResolver = fireResolver;
            _stage = stage;
            _breakBombRecoveryDuration = balance.BreakBombRecoveryDuration;
            _bombMinFlightDistance = balance.BombMinFlightDistance;
            _spreadService = spreadService;
            _fireSpreadInterval = balance.FireBombSpreadInterval;
            _breakSpreadInterval = balance.BreakBombSpreadInterval;
        }

        public BombSpec CreateBreakBombSpec(PlayerBuild build)
        {
            return new BombSpec(
                BombType.Break,
                build.BreakFlightRange,
                _bombMinFlightDistance,
                build.BreakEffectRange,
                build.BreakDamage,
                build.BreakCooldown,
                true, // ブレークボムは常に壁貫通
                0f,
                build.BreakCollapseTime,
                _breakBombRecoveryDuration,
                build.HasBreakBombPenetration);
        }

        public BombSpec CreateFireBombSpec(PlayerBuild build)
        {
            return new BombSpec(
                BombType.Fire,
                build.FireFlightRange,
                _bombMinFlightDistance,
                build.FireEffectRange,
                build.FireDamage,
                build.FireCooldown,
                build.FireWallPenetration,
                build.FireDuration,
                0f,
                0f,
                build.HasFireBombPenetration);
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
                case BombType.Break:
                    ExecuteBreakBomb(landingPos, cmd.Spec, players, owner);
                    break;
                case BombType.Fire:
                    ExecuteFireBomb(landingPos, cmd.Spec, players, owner);
                    break;
            }
        }

        private void ExecuteBreakBomb(GridPos landingPos, BombSpec spec, IReadOnlyList<PlayerModel> players, PlayerModel owner)
        {
            var result = _breakResolver.Resolve(landingPos, spec, _stage);
            _spreadService.EnqueueBreakBomb(result, landingPos, players, owner, _breakSpreadInterval);
        }

        private void ExecuteFireBomb(GridPos landingPos, BombSpec spec, IReadOnlyList<PlayerModel> players, PlayerModel owner)
        {
            var result = _fireResolver.Resolve(landingPos, spec, _stage);
            _spreadService.EnqueueFireBomb(result, landingPos, players, owner, _fireSpreadInterval);
        }
    }
}
