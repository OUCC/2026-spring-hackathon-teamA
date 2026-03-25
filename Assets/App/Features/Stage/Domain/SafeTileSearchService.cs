using System.Collections.Generic;
using FloorBreaker.Shared.Domain.Grid;

namespace FloorBreaker.Stage.Domain
{
    public sealed class SafeTileSearchService
    {
        /// <summary>
        /// BFS で from から最も近い安全マスを探す。
        /// Safe = IsPassable かつ occupied に含まれない。
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

                    var state = model.GetTileState(neighbor);
                    if (state == TileState.PermanentlyDestroyed) continue;

                    visited.Add(neighbor);
                    queue.Enqueue(neighbor);
                }
            }

            return null;
        }

        private static bool IsSafe(StageModel model, GridPos pos, HashSet<GridPos> occupied)
        {
            if (occupied != null && occupied.Contains(pos)) return false;
            return model.IsPassable(pos);
        }
    }
}
