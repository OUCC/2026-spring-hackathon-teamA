using System;
using R3;
using FloorBreaker.Shared.Domain.Grid;

namespace FloorBreaker.Stage.Domain
{
    public readonly struct TileChangedEvent
    {
        public readonly GridPos Pos;
        public readonly TileData OldData;
        public readonly TileData NewData;

        public TileChangedEvent(GridPos pos, TileData oldData, TileData newData)
        {
            Pos = pos;
            OldData = oldData;
            NewData = newData;
        }

        // Presentation 層の switch 用ショートカット
        public TileCondition OldCondition => OldData.Condition;
        public TileCondition NewCondition => NewData.Condition;
        public TileType NewType => NewData.Type;
    }

    public sealed class StageModel : IDisposable
    {
        private readonly TileData[,] _tiles;
        private readonly TileCoordRange _initialBounds;
        private readonly Subject<TileChangedEvent> _tileChanged = new();

        public StageBounds Bounds { get; }
        public Observable<TileChangedEvent> TileChanged => _tileChanged;

        public StageModel(TileCoordRange initialBounds)
        {
            _initialBounds = initialBounds;
            Bounds = new StageBounds(initialBounds);
            _tiles = new TileData[initialBounds.Width, initialBounds.Height];

            // 全タイルを Default (Normal + Intact) で初期化
            for (int x = 0; x < initialBounds.Width; x++)
                for (int y = 0; y < initialBounds.Height; y++)
                    _tiles[x, y] = TileData.Default;
        }

        public TileData GetTileData(GridPos pos)
        {
            if (!IsInBounds(pos))
                return new TileData { Type = TileType.Normal, Condition = TileCondition.PermanentlyDestroyed, WarpPairId = -1 };
            return _tiles[pos.X - _initialBounds.MinX, pos.Y - _initialBounds.MinY];
        }

        public TileType GetTileType(GridPos pos) => GetTileData(pos).Type;
        public TileCondition GetTileCondition(GridPos pos) => GetTileData(pos).Condition;

        public void SetTileData(GridPos pos, TileData data)
        {
            if (!IsInBounds(pos)) return;
            int ix = pos.X - _initialBounds.MinX;
            int iy = pos.Y - _initialBounds.MinY;
            var old = _tiles[ix, iy];
            if (old.Type == data.Type && old.Condition == data.Condition && old.WarpPairId == data.WarpPairId) return;
            _tiles[ix, iy] = data;
            _tileChanged.OnNext(new TileChangedEvent(pos, old, data));
        }

        public void SetTileCondition(GridPos pos, TileCondition condition)
        {
            if (!IsInBounds(pos)) return;
            int ix = pos.X - _initialBounds.MinX;
            int iy = pos.Y - _initialBounds.MinY;
            var old = _tiles[ix, iy];
            if (old.Condition == condition) return;
            var newData = old;
            newData.Condition = condition;
            _tiles[ix, iy] = newData;
            _tileChanged.OnNext(new TileChangedEvent(pos, old, newData));
        }

        public bool IsPassable(GridPos pos) => GetTileData(pos).IsPassable;

        public bool IsInBounds(GridPos pos) => _initialBounds.Contains(pos);

        public int GetAliveTileCount()
        {
            int count = 0;
            for (int x = 0; x < _initialBounds.Width; x++)
                for (int y = 0; y < _initialBounds.Height; y++)
                    if (_tiles[x, y].Condition != TileCondition.PermanentlyDestroyed)
                        count++;
            return count;
        }

        public TileCoordRange GetCurrentBounds() => Bounds.Current;

        /// <summary>タイルデータの内部配列への直接参照（読み取り用スナップショット生成向け）。</summary>
        internal TileData[,] GetTilesRaw() => _tiles;

        public void Dispose()
        {
            _tileChanged.Dispose();
        }
    }
}
