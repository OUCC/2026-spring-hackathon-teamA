using System;
using System.Collections.Generic;
using FloorBreaker.Shared.Domain.Grid;
using FloorBreaker.Shared.Domain.Primitives;
using FloorBreaker.Shared.Application.Interfaces;
using FloorBreaker.Stage.Domain;
using FloorBreaker.Player.Domain;
using FloorBreaker.Bombs.Domain;
using FloorBreaker.Slimes.Domain;
using FloorBreaker.Upgrades.Domain;

namespace FloorBreaker.Bombs.Application
{
    public sealed class BombLaunchUseCase
    {
        private readonly BombLandingResolver _landingResolver;
        private readonly FallBombResolver _fallResolver;
        private readonly FireBombResolver _fireResolver;
        private readonly StageModel _stage;
        private readonly TileTimerService _tileTimerService;
        private readonly PlayerDamageService _damageService;
        private readonly SafeTileSearchService _safeTileSearch;
        private readonly SlimeRegistry _slimeRegistry;
        private readonly SlimeDropResolver _slimeDropResolver;
        private readonly IRandomProvider _random;
        private readonly float _fallBombRecoveryDuration;

        public BombLaunchUseCase(
            BombLandingResolver landingResolver,
            FallBombResolver fallResolver,
            FireBombResolver fireResolver,
            StageModel stage,
            TileTimerService tileTimerService,
            PlayerDamageService damageService,
            SafeTileSearchService safeTileSearch,
            IBalanceParameters balance,
            SlimeRegistry slimeRegistry = null,
            SlimeDropResolver slimeDropResolver = null,
            IRandomProvider random = null)
        {
            _landingResolver = landingResolver;
            _fallResolver = fallResolver;
            _fireResolver = fireResolver;
            _stage = stage;
            _tileTimerService = tileTimerService;
            _damageService = damageService;
            _safeTileSearch = safeTileSearch;
            _fallBombRecoveryDuration = balance.FallBombRecoveryDuration;
            _slimeRegistry = slimeRegistry;
            _slimeDropResolver = slimeDropResolver;
            _random = random;
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
            var affectedSet = new HashSet<GridPos>(result.AffectedTiles);

            // 1. 壁破壊
            foreach (var wall in result.WallsDestroyed)
                _stage.SetTileState(wall, TileState.Normal);

            // 2. タイルを Collapsing に設定 + タイマー開始
            foreach (var tile in result.AffectedTiles)
            {
                _stage.SetTileState(tile, TileState.Collapsing);
                _tileTimerService.StartCollapseTimer(tile, result.CollapseTime, result.RecoveryTime);
            }

            // 3. 影響範囲内のプレイヤーにダメージ（強制移動あり）
            var occupied = BuildOccupiedSet(players);
            foreach (var player in players)
            {
                if (affectedSet.Contains(player.CurrentPosition))
                {
                    _damageService.ApplyDamage(
                        player, result.Damage, true,
                        _stage, _safeTileSearch, occupied);
                }
            }

            // 4. 影響範囲内のスライム死亡 + ドロップ
            KillSlimesInArea(result.AffectedTiles, owner);
        }

        private void ExecuteFireBomb(GridPos landingPos, BombSpec spec, IReadOnlyList<PlayerModel> players, PlayerModel owner)
        {
            var result = _fireResolver.Resolve(landingPos, spec, _stage);
            var affectedSet = new HashSet<GridPos>(result.AffectedTiles);

            // 1. 壁破壊
            foreach (var wall in result.WallsDestroyed)
                _stage.SetTileState(wall, TileState.Normal);

            // 2. タイルを OnFire に設定 + 炎タイマー開始
            foreach (var tile in result.AffectedTiles)
            {
                _stage.SetTileState(tile, TileState.OnFire);
                _tileTimerService.StartFireTimer(tile, result.FireDuration);
            }

            // 3. 影響範囲内のプレイヤーに接触ダメージ（強制移動なし）
            var occupied = BuildOccupiedSet(players);
            foreach (var player in players)
            {
                if (affectedSet.Contains(player.CurrentPosition))
                {
                    _damageService.ApplyDamage(
                        player, result.ContactDamage, false,
                        _stage, _safeTileSearch, occupied);
                }
            }

            // 4. 影響範囲内のスライム死亡 + ドロップ
            KillSlimesInArea(result.AffectedTiles, owner);
        }

        private void KillSlimesInArea(IReadOnlyList<GridPos> affectedTiles, PlayerModel killer)
        {
            if (_slimeRegistry == null || _slimeDropResolver == null) return;

            var slimes = _slimeRegistry.GetSlimesAt(affectedTiles);
            foreach (var slime in slimes)
            {
                slime.Kill();
                _slimeDropResolver.Resolve(slime, killer, _random);
                _slimeRegistry.Remove(slime.Id);
            }
        }

        private static HashSet<GridPos> BuildOccupiedSet(IReadOnlyList<PlayerModel> players)
        {
            var set = new HashSet<GridPos>();
            foreach (var p in players)
                set.Add(p.CurrentPosition);
            return set;
        }
    }
}
