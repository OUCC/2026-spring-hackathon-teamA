using System;
using System.Collections.Generic;

namespace FloorBreaker.Shared.Domain.Grid
{
    public readonly struct TileCoordRange : IEquatable<TileCoordRange>
    {
        public readonly int MinX;
        public readonly int MinY;
        public readonly int MaxX;
        public readonly int MaxY;

        public TileCoordRange(int minX, int minY, int maxX, int maxY)
        {
            MinX = minX;
            MinY = minY;
            MaxX = maxX;
            MaxY = maxY;
        }

        public static TileCoordRange FromSize(int size) => new(0, 0, size - 1, size - 1);

        public int Width => MaxX - MinX + 1;
        public int Height => MaxY - MinY + 1;
        public int TileCount => Width * Height;

        public bool Contains(GridPos pos)
        {
            return pos.X >= MinX && pos.X <= MaxX && pos.Y >= MinY && pos.Y <= MaxY;
        }

        public TileCoordRange Shrink(int amount)
        {
            return new TileCoordRange(MinX + amount, MinY + amount, MaxX - amount, MaxY - amount);
        }

        public IEnumerable<GridPos> GetAllPositions()
        {
            for (int y = MinY; y <= MaxY; y++)
                for (int x = MinX; x <= MaxX; x++)
                    yield return new GridPos(x, y);
        }

        public IReadOnlyList<GridPos> GetOuterRing()
        {
            var ring = new List<GridPos>();

            // Top row
            for (int x = MinX; x <= MaxX; x++)
                ring.Add(new GridPos(x, MaxY));
            // Bottom row
            for (int x = MinX; x <= MaxX; x++)
                ring.Add(new GridPos(x, MinY));
            // Left column (excluding corners)
            for (int y = MinY + 1; y < MaxY; y++)
                ring.Add(new GridPos(MinX, y));
            // Right column (excluding corners)
            for (int y = MinY + 1; y < MaxY; y++)
                ring.Add(new GridPos(MaxX, y));

            return ring;
        }

        public bool Equals(TileCoordRange other) =>
            MinX == other.MinX && MinY == other.MinY && MaxX == other.MaxX && MaxY == other.MaxY;
        public override bool Equals(object obj) => obj is TileCoordRange other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(MinX, MinY, MaxX, MaxY);
        public static bool operator ==(TileCoordRange a, TileCoordRange b) => a.Equals(b);
        public static bool operator !=(TileCoordRange a, TileCoordRange b) => !a.Equals(b);

        public override string ToString() => $"[({MinX},{MinY})-({MaxX},{MaxY})]";
    }
}
