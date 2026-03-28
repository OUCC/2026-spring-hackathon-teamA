using System;
using System.Collections.Generic;
using FloorBreaker.Shared.Domain.Grid;
using FloorBreaker.Shared.Application.Interfaces;
using FloorBreaker.Stage.Domain;
using FloorBreaker.Player.Domain;

namespace FloorBreaker.Slimes.Domain
{
    public sealed class SlimeSpawnService
    {
        private readonly StageModel _stage;
        private readonly SlimeRegistry _registry;
        private readonly IReadOnlyList<PlayerModel> _players;
        private readonly IRandomProvider _random;
        private readonly IBalanceParameters _balance;
        private int _nextIdCounter;

        public SlimeSpawnService(
            StageModel stage,
            SlimeRegistry registry,
            IReadOnlyList<PlayerModel> players,
            IRandomProvider random,
            IBalanceParameters balance)
        {
            _stage = stage;
            _registry = registry;
            _players = players;
            _random = random;
            _balance = balance;
        }

        public IReadOnlyList<SlimeModel> SpawnIfNeeded()
        {
            int aliveTiles = _stage.GetAliveTileCount();
            int targetCount = (int)(aliveTiles * _balance.SlimeTargetRatio + 0.001f);
            int deficit = targetCount - _registry.AliveCount;

            if (deficit <= 0)
                return Array.Empty<SlimeModel>();

            var candidates = BuildCandidates(_stage, _registry, _players, _balance.SlimeMinDistanceFromPlayer);
            if (candidates.Count == 0)
                return Array.Empty<SlimeModel>();

            var spawned = new List<SlimeModel>();
            int totalRatio = _balance.SlimeSpawnRatioNormal + _balance.SlimeSpawnRatioGold + _balance.SlimeSpawnRatioRed;

            for (int i = 0; i < deficit && candidates.Count > 0; i++)
            {
                int posIdx = _random.Range(0, candidates.Count);
                var pos = candidates[posIdx];
                candidates.RemoveAt(posIdx);

                var type = RollSlimeType(_random, _balance, totalRatio);
                var slime = new SlimeModel(new SlimeId(++_nextIdCounter), type, pos, _balance.SlimeAttackCooldown);
                _registry.Add(slime);
                spawned.Add(slime);
            }

            return spawned;
        }

        private static List<GridPos> BuildCandidates(
            StageModel stage,
            SlimeRegistry registry,
            IReadOnlyList<PlayerModel> players,
            int minDistance)
        {
            var candidates = new List<GridPos>();

            foreach (var pos in stage.GetCurrentBounds().GetAllPositions())
            {
                if (!stage.IsPassable(pos)) continue;
                if (registry.IsOccupied(pos)) continue;

                bool tooClose = false;
                foreach (var player in players)
                {
                    if (pos.ChebyshevDistance(player.CurrentPosition) < minDistance)
                    {
                        tooClose = true;
                        break;
                    }
                }
                if (tooClose) continue;

                candidates.Add(pos);
            }

            return candidates;
        }

        private static SlimeType RollSlimeType(IRandomProvider random, IBalanceParameters balance, int totalRatio)
        {
            int roll = random.Range(0, totalRatio);
            if (roll < balance.SlimeSpawnRatioNormal)
                return SlimeType.Normal;
            if (roll < balance.SlimeSpawnRatioNormal + balance.SlimeSpawnRatioGold)
                return SlimeType.Gold;
            return SlimeType.Red;
        }
    }
}
