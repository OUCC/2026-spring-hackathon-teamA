using System;
using R3;
using FloorBreaker.Shared.Domain.Grid;

namespace FloorBreaker.Stage.Domain
{
    public readonly struct TileChangedEvent
    {
        public readonly GridPos Pos;
        public readonly TileState OldState;
        public readonly TileState NewState;

        public TileChangedEvent(GridPos pos, TileState oldState, TileState newState)
        {
            Pos = pos;
            OldState = oldState;
            NewState = newState;
        }
    }

    public sealed class StageModel : IDisposable
    {
        private readonly TileState[,] _tiles;
        private readonly TileCoordRange _initialBounds;
        private readonly Subject<TileChangedEvent> _tileChanged = new();

        public StageBounds Bounds { get; }
        public Observable<TileChangedEvent> TileChanged => _tileChanged;

        public StageModel(TileCoordRange initialBounds)
        {
            _initialBounds = initialBounds;
            Bounds = new StageBounds(initialBounds);
            _tiles = new TileState[initialBounds.Width, initialBounds.Height];
        }

        public TileState GetTileState(GridPos pos)
        {
            if (!IsInBounds(pos)) return TileState.PermanentlyDestroyed;
            return _tiles[pos.X - _initialBounds.MinX, pos.Y - _initialBounds.MinY];
        }

        public void SetTileState(GridPos pos, TileState state)
        {
            if (!IsInBounds(pos)) return;

            int ix = pos.X - _initialBounds.MinX;
            int iy = pos.Y - _initialBounds.MinY;
            var old = _tiles[ix, iy];
            if (old == state) return;

            _tiles[ix, iy] = state;
            _tileChanged.OnNext(new TileChangedEvent(pos, old, state));
        }

        public bool IsPassable(GridPos pos)
        {
            var state = GetTileState(pos);
            return state == TileState.Normal || state == TileState.OnFire;
        }

        public bool IsInBounds(GridPos pos) => _initialBounds.Contains(pos);

        public int GetAliveTileCount()
        {
            int count = 0;
            for (int x = 0; x < _initialBounds.Width; x++)
                for (int y = 0; y < _initialBounds.Height; y++)
                    if (_tiles[x, y] != TileState.PermanentlyDestroyed)
                        count++;
            return count;
        }

        public TileCoordRange GetCurrentBounds() => Bounds.Current;

        public void Dispose()
        {
            _tileChanged.Dispose();
        }
    }
}
