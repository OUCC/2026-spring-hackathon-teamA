using System.Collections.Generic;
using FloorBreaker.Shared.Domain.Grid;

namespace FloorBreaker.Slimes.Domain
{
    public sealed class SlimeRegistry
    {
        private readonly Dictionary<SlimeId, SlimeModel> _slimes = new();
        private readonly Dictionary<GridPos, SlimeId> _positionIndex = new();

        public int AliveCount => _slimes.Count;

        public void Add(SlimeModel slime)
        {
            _slimes[slime.Id] = slime;
            _positionIndex[slime.Position] = slime.Id;
        }

        public void Remove(SlimeId id)
        {
            if (_slimes.TryGetValue(id, out var slime))
            {
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
        }
    }
}
