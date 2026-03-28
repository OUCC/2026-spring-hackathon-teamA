using System.Collections.Generic;
using FloorBreaker.Shared.Domain.Grid;

namespace FloorBreaker.Stage.Domain
{
    /// <summary>
    /// ガスタイルの連鎖引火を管理する。
    /// 炎ボムがガスタイルに着弾した際、BFS で隣接ガスに段階的に延焼する。
    /// </summary>
    public sealed class GasIgnitionService
    {
        private readonly StageModel _stage;
        private readonly TileTimerService _tileTimerService;
        private readonly float _chainDelayPerStep;
        private readonly float _fireDuration;

        private readonly List<PendingIgnition> _pending = new();

        public bool HasPending => _pending.Count > 0;

        public GasIgnitionService(
            StageModel stage,
            TileTimerService tileTimerService,
            float chainDelayPerStep,
            float fireDuration)
        {
            _stage = stage;
            _tileTimerService = tileTimerService;
            _chainDelayPerStep = chainDelayPerStep;
            _fireDuration = fireDuration;
        }

        /// <summary>
        /// 指定位置から BFS でガス連鎖引火をスケジュールする。
        /// origin 自体は既に OnFire になっている前提。
        /// </summary>
        public void Ignite(GridPos origin)
        {
            var visited = new HashSet<GridPos> { origin };
            var queue = new Queue<(GridPos pos, int depth)>();

            // origin の4方向隣接をシードに
            foreach (var neighbor in origin.Neighbors4())
            {
                if (!_stage.IsInBounds(neighbor)) continue;
                if (visited.Contains(neighbor)) continue;

                var data = _stage.GetTileData(neighbor);
                if (data.Type == TileType.Gas && data.Condition == TileCondition.Intact)
                {
                    visited.Add(neighbor);
                    queue.Enqueue((neighbor, 1));
                }
            }

            // BFS で隣接ガスを探索、遅延付きでスケジュール
            while (queue.Count > 0)
            {
                var (pos, depth) = queue.Dequeue();
                float delay = depth * _chainDelayPerStep;
                _pending.Add(new PendingIgnition(pos, delay));

                foreach (var neighbor in pos.Neighbors4())
                {
                    if (!_stage.IsInBounds(neighbor)) continue;
                    if (visited.Contains(neighbor)) continue;

                    var data = _stage.GetTileData(neighbor);
                    if (data.Type == TileType.Gas && data.Condition == TileCondition.Intact)
                    {
                        visited.Add(neighbor);
                        queue.Enqueue((neighbor, depth + 1));
                    }
                }
            }
        }

        /// <summary>
        /// 毎フレーム呼び出し。遅延を消化し、タイルに火を付ける。
        /// </summary>
        public void Tick(float deltaTime)
        {
            if (_pending.Count == 0) return;

            for (int i = _pending.Count - 1; i >= 0; i--)
            {
                var p = _pending[i];
                p.RemainingDelay -= deltaTime;
                _pending[i] = p;

                if (p.RemainingDelay <= 0f)
                {
                    // まだガスで Intact なら引火
                    var data = _stage.GetTileData(p.Pos);
                    if (data.Type == TileType.Gas && data.Condition == TileCondition.Intact)
                    {
                        _stage.SetTileCondition(p.Pos, TileCondition.OnFire);
                        _tileTimerService.StartFireTimer(p.Pos, _fireDuration);
                    }

                    _pending.RemoveAt(i);
                }
            }
        }

        private struct PendingIgnition
        {
            public GridPos Pos;
            public float RemainingDelay;

            public PendingIgnition(GridPos pos, float delay)
            {
                Pos = pos;
                RemainingDelay = delay;
            }
        }
    }
}
