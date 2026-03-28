using System;
using System.Collections.Generic;
using FloorBreaker.Shared.Domain.Grid;

namespace FloorBreaker.Stage.Domain
{
    public sealed class SafeTileSearchService
    {
        /// <summary>
        /// BFS で from から最も近い安全マスを探す。
        /// Safe = IsPassable かつ occupied に含まれない。
        /// BFS で見つからない場合、ステージ全体から中央に最も近い安全タイルを探すフォールバック付き。
        /// </summary>
        public GridPos? FindSafeTile(StageModel model, GridPos from, HashSet<GridPos> occupied)
        {
            var visited = new HashSet<GridPos> { from };
            var queue = new Queue<GridPos>();
            queue.Enqueue(from);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();

                // from 自体はスキップ（崩落中の想定）、それ以外で安全なら返す
                if (current != from && IsSafe(model, current, occupied))
                    return current;

                foreach (var neighbor in current.Neighbors4())
                {
                    if (!model.IsInBounds(neighbor)) continue;
                    if (visited.Contains(neighbor)) continue;

                    var cond = model.GetTileCondition(neighbor);
                    if (cond == TileCondition.PermanentlyDestroyed) continue;

                    visited.Add(neighbor);
                    queue.Enqueue(neighbor);
                }
            }

            // フォールバック: BFS で到達不能な場合、ステージ全体から中央に最も近い安全タイルを探す
            return FindSafeTileFallback(model, occupied);
        }

        private static GridPos? FindSafeTileFallback(StageModel model, HashSet<GridPos> occupied)
        {
            var bounds = model.GetCurrentBounds();
            int centerX = (bounds.MinX + bounds.MaxX) / 2;
            int centerY = (bounds.MinY + bounds.MaxY) / 2;

            GridPos? best = null;
            int bestDist = int.MaxValue;

            for (int x = bounds.MinX; x <= bounds.MaxX; x++)
            {
                for (int y = bounds.MinY; y <= bounds.MaxY; y++)
                {
                    var pos = new GridPos(x, y);
                    if (!IsSafe(model, pos, occupied)) continue;
                    int dist = Math.Abs(x - centerX) + Math.Abs(y - centerY);
                    if (dist < bestDist)
                    {
                        best = pos;
                        bestDist = dist;
                    }
                }
            }

            return best;
        }

        private static bool IsSafe(StageModel model, GridPos pos, HashSet<GridPos> occupied)
        {
            if (occupied != null && occupied.Contains(pos)) return false;
            return model.IsPassable(pos);
        }
    }
}
