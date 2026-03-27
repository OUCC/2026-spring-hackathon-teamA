using System;
using System.Collections.Generic;
using FloorBreaker.Shared.Domain.Grid;
using FloorBreaker.Shared.Application.Interfaces;
using FloorBreaker.Stage.Domain;
using FloorBreaker.Player.Domain;
using FloorBreaker.Player.Application;

namespace FloorBreaker.Slimes.Domain
{
    public sealed class SlimeAiService
    {
        private readonly PlayerDamageService _damageService;
        private readonly SafeTileSearchService _safeTileSearch;
        private readonly SlimeRegistry _registry;
        private readonly IReadOnlyList<PlayerModel> _players;
        private readonly StageModel _stage;
        private readonly IBalanceParameters _balance;

        public SlimeAiService(
            PlayerDamageService damageService,
            SafeTileSearchService safeTileSearch,
            SlimeRegistry registry,
            IReadOnlyList<PlayerModel> players,
            StageModel stage,
            IBalanceParameters balance)
        {
            _damageService = damageService;
            _safeTileSearch = safeTileSearch;
            _registry = registry;
            _players = players;
            _stage = stage;
            _balance = balance;
        }

        public void TickAll(float deltaTime)
        {
            float slimeSpeed = _balance.BaseMovementSpeed * _balance.SlimeSpeedMultiplier;
            float moveThreshold = 1f; // 1マス分のアキュムレータ閾値

            // コピーして反復（Tick 中に Remove が走る可能性は低いがガード）
            var slimes = new List<SlimeModel>(_registry.GetAll());

            foreach (var slime in slimes)
            {
                if (!slime.IsAlive) continue;

                slime.Tick(deltaTime);

                var nearest = FindNearestPlayer(slime.Position, _players, _balance.SlimeDetectionRange);
                if (nearest == null) continue;

                int dist = slime.Position.ChebyshevDistance(nearest.CurrentPosition);

                // 隣接（4方向）: 攻撃
                if (dist == 1 && IsCardinalAdjacent(slime.Position, nearest.CurrentPosition))
                {
                    // 初めて隣接した瞬間は溜め時間を設ける（即攻撃を防ぐ）
                    if (!slime.WasAdjacentToPlayer)
                    {
                        slime.WasAdjacentToPlayer = true;
                        slime.ResetAttackCooldown(_balance.SlimeAttackCooldown);
                        continue;
                    }

                    if (slime.CanAttack)
                    {
                        var occupied = BuildOccupiedSet(_players);
                        _damageService.ApplyDamage(
                            nearest, _balance.SlimeAttackDamage, false, occupied);
                        slime.ResetAttackCooldown(_balance.SlimeAttackCooldown);
                        _registry.NotifyAttack(slime.Id, slime.Position, nearest.CurrentPosition);
                    }
                    continue;
                }
                else
                {
                    slime.WasAdjacentToPlayer = false;
                }

                // 移動アキュムレータ
                slime.MoveAccumulator += deltaTime * slimeSpeed;
                if (slime.MoveAccumulator < moveThreshold) continue;

                slime.MoveAccumulator -= moveThreshold;

                // 貪欲法で1マス移動
                var target = PickMoveTarget(slime.Position, nearest.CurrentPosition, _stage, _registry);
                if (target.HasValue)
                {
                    var oldPos = slime.Position;
                    slime.MoveTo(target.Value);
                    _registry.UpdatePosition(slime, oldPos, target.Value);
                }
            }
        }

        private static PlayerModel FindNearestPlayer(GridPos from, IReadOnlyList<PlayerModel> players, int detectionRange)
        {
            PlayerModel nearest = null;
            int minDist = int.MaxValue;

            foreach (var player in players)
            {
                if (player.Stats.IsDead) continue;
                int d = from.ChebyshevDistance(player.CurrentPosition);
                if (d <= detectionRange && d < minDist)
                {
                    minDist = d;
                    nearest = player;
                }
            }

            return nearest;
        }

        private static bool IsCardinalAdjacent(GridPos a, GridPos b)
        {
            int dx = Math.Abs(a.X - b.X);
            int dy = Math.Abs(a.Y - b.Y);
            return (dx + dy) == 1;
        }

        private static GridPos? PickMoveTarget(GridPos from, GridPos toward, StageModel stage, SlimeRegistry registry)
        {
            int dx = toward.X - from.X;
            int dy = toward.Y - from.Y;

            GridPos? primary = null;
            GridPos? secondary = null;

            if (Math.Abs(dx) >= Math.Abs(dy))
            {
                primary = new GridPos(from.X + Math.Sign(dx), from.Y);
                secondary = dy != 0 ? new GridPos(from.X, from.Y + Math.Sign(dy)) : null;
            }
            else
            {
                primary = new GridPos(from.X, from.Y + Math.Sign(dy));
                secondary = dx != 0 ? new GridPos(from.X + Math.Sign(dx), from.Y) : null;
            }

            if (primary.HasValue && CanMoveTo(primary.Value, stage, registry))
                return primary.Value;
            if (secondary.HasValue && CanMoveTo(secondary.Value, stage, registry))
                return secondary.Value;

            return null;
        }

        private static bool CanMoveTo(GridPos pos, StageModel stage, SlimeRegistry registry)
        {
            return stage.IsPassable(pos) && !registry.IsOccupied(pos);
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
