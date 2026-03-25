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
        public IReadOnlyList<SlimeModel> SpawnIfNeeded(
            StageModel stage,
            SlimeRegistry registry,
            IReadOnlyList<PlayerModel> players,
            IRandomProvider random,
            IBalanceParameters balance)
        {
            int aliveTiles = stage.GetAliveTileCount();
            int targetCount = (int)(aliveTiles * balance.SlimeTargetRatio);
            int deficit = targetCount - registry.AliveCount;

            if (deficit <= 0)
                return Array.Empty<SlimeModel>();

            var candidates = BuildCandidates(stage, registry, players, balance.SlimeMinDistanceFromPlayer);
            if (candidates.Count == 0)
                return Array.Empty<SlimeModel>();

            var spawned = new List<SlimeModel>();
            int totalRatio = balance.SlimeSpawnRatioNormal + balance.SlimeSpawnRatioGold + balance.SlimeSpawnRatioRed;

            for (int i = 0; i < deficit && candidates.Count > 0; i++)
            {
                int posIdx = random.Range(0, candidates.Count);
                var pos = candidates[posIdx];
                candidates.RemoveAt(posIdx);

                var type = RollSlimeType(random, balance, totalRatio);
                var slime = new SlimeModel(SlimeId.Next(), type, pos);
                registry.Add(slime);
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
