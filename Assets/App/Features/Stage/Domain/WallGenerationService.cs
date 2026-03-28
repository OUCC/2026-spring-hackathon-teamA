using System;
using System.Collections.Generic;
using FloorBreaker.Shared.Application.Interfaces;
using FloorBreaker.Shared.Domain.Grid;

namespace FloorBreaker.Stage.Domain
{
    public sealed class WallGenerationService
    {
        private readonly float _seedPercent;
        private readonly float _growthChance;
        private readonly float _targetPercent;
        private readonly int _protectionRadius;

        public WallGenerationService(
            float wallSeedPercent,
            float wallGrowthChance,
            float wallTargetPercent,
            int spawnProtectionRadius)
        {
            _seedPercent = wallSeedPercent;
            _growthChance = wallGrowthChance;
            _targetPercent = wallTargetPercent;
            _protectionRadius = spawnProtectionRadius;
        }

        public HashSet<GridPos> Generate(
            TileCoordRange bounds,
            IReadOnlyList<GridPos> spawnPositions,
            IRandomProvider random)
        {
            var exclusion = BuildExclusionSet(bounds, spawnPositions);
            int targetCount = (int)(bounds.TileCount * _targetPercent);

            // Seed pass
            var walls = new HashSet<GridPos>();
            foreach (var pos in bounds.GetAllPositions())
            {
                if (exclusion.Contains(pos)) continue;
                if (random.Chance(_seedPercent))
                    walls.Add(pos);
            }

            // Growth pass
            int maxIterations = 100;
            for (int iter = 0; iter < maxIterations && walls.Count < targetCount; iter++)
            {
                var newWalls = new List<GridPos>();
                foreach (var wall in walls)
                {
                    foreach (var neighbor in wall.Neighbors4())
                    {
                        if (!bounds.Contains(neighbor)) continue;
                        if (exclusion.Contains(neighbor)) continue;
                        if (walls.Contains(neighbor)) continue;
                        if (random.Chance(_growthChance))
                            newWalls.Add(neighbor);
                    }
                }

                if (newWalls.Count == 0) break;

                foreach (var pos in newWalls)
                {
                    walls.Add(pos);
                    if (walls.Count >= targetCount) break;
                }
            }

            return walls;
        }

        private HashSet<GridPos> BuildExclusionSet(TileCoordRange bounds, IReadOnlyList<GridPos> spawnPositions)
        {
            var exclusion = new HashSet<GridPos>();
            foreach (var spawn in spawnPositions)
                AddProtectionZone(exclusion, bounds, spawn);
            return exclusion;
        }

        private void AddProtectionZone(HashSet<GridPos> exclusion, TileCoordRange bounds, GridPos center)
        {
            for (int dx = -_protectionRadius; dx <= _protectionRadius; dx++)
            {
                for (int dy = -_protectionRadius; dy <= _protectionRadius; dy++)
                {
                    var pos = new GridPos(center.X + dx, center.Y + dy);
                    if (bounds.Contains(pos))
                        exclusion.Add(pos);
                }
            }
        }
    }
}
