using System;
using System.Collections.Generic;
using R3;
using FloorBreaker.Shared.Domain.Grid;

namespace FloorBreaker.Slimes.Domain
{
    public sealed class SlimeRegistry : IDisposable
    {
        private readonly Dictionary<SlimeId, SlimeModel> _slimes = new();
        private readonly Dictionary<GridPos, SlimeId> _positionIndex = new();

        private readonly Subject<SlimeSpawnedEvent> _spawned = new();
        private readonly Subject<SlimeMovedEvent> _moved = new();
        private readonly Subject<SlimeKilledEvent> _killed = new();
        private readonly Subject<SlimeAttackedEvent> _attacked = new();

        public Observable<SlimeSpawnedEvent> Spawned => _spawned;
        public Observable<SlimeMovedEvent> Moved => _moved;
        public Observable<SlimeKilledEvent> Killed => _killed;
        public Observable<SlimeAttackedEvent> Attacked => _attacked;

        public int AliveCount => _slimes.Count;

        public void Add(SlimeModel slime)
        {
            _slimes[slime.Id] = slime;
            _positionIndex[slime.Position] = slime.Id;
            _spawned.OnNext(new SlimeSpawnedEvent(slime.Id, slime.Type, slime.Position));
        }

        public void Remove(SlimeId id)
        {
            if (_slimes.TryGetValue(id, out var slime))
            {
                _killed.OnNext(new SlimeKilledEvent(slime.Id, slime.Type, slime.Position));
                _positionIndex.Remove(slime.Position);
                _slimes.Remove(id);
            }
        }

        public SlimeModel GetAt(GridPos pos)
        {
            if (_positionIndex.TryGetValue(pos, out var id) && _slimes.TryGetValue(id, out var slime))
                return slime;
            return null;
        }

        public bool IsOccupied(GridPos pos) => _positionIndex.ContainsKey(pos);

        public IReadOnlyCollection<SlimeModel> GetAll() => _slimes.Values;

        public IReadOnlyList<SlimeModel> GetSlimesAt(IEnumerable<GridPos> positions)
        {
            var result = new List<SlimeModel>();
            foreach (var pos in positions)
            {
                var slime = GetAt(pos);
                if (slime != null && slime.IsAlive)
                    result.Add(slime);
            }
            return result;
        }

        /// <summary>
        /// スライム移動時に位置インデックスを更新する。
        /// </summary>
        public void UpdatePosition(SlimeModel slime, GridPos oldPos, GridPos newPos)
        {
            _positionIndex.Remove(oldPos);
            _positionIndex[newPos] = slime.Id;
            _moved.OnNext(new SlimeMovedEvent(slime.Id, oldPos, newPos));
        }

        /// <summary>
        /// スライム攻撃時に呼び出し、攻撃イベントを発火する。
        /// </summary>
        public void NotifyAttack(SlimeId attackerId, GridPos attackerPosition, GridPos targetPosition)
        {
            _attacked.OnNext(new SlimeAttackedEvent(attackerId, attackerPosition, targetPosition));
        }

        public void Dispose()
        {
            _spawned.Dispose();
            _moved.Dispose();
            _killed.Dispose();
            _attacked.Dispose();
        }
    }
}
