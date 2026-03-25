using System;
using FloorBreaker.Shared.Domain.Primitives;

namespace FloorBreaker.Shared.Domain.Grid
{
    public readonly struct GridPos : IEquatable<GridPos>
    {
        public readonly int X;
        public readonly int Y;

        public GridPos(int x, int y)
        {
            X = x;
            Y = y;
        }

        public static GridPos operator +(GridPos a, GridPos b) => new(a.X + b.X, a.Y + b.Y);
        public static GridPos operator -(GridPos a, GridPos b) => new(a.X - b.X, a.Y - b.Y);
        public static GridPos operator *(GridPos a, int s) => new(a.X * s, a.Y * s);

        public GridPos Neighbor(Direction8 dir) => this + dir.ToOffset();

        public GridPos[] Neighbors4()
        {
            return new[]
            {
                new GridPos(X, Y + 1),
                new GridPos(X + 1, Y),
                new GridPos(X, Y - 1),
                new GridPos(X - 1, Y),
            };
        }

        public GridPos[] Neighbors8()
        {
            return new[]
            {
                new GridPos(X, Y + 1),
                new GridPos(X + 1, Y + 1),
                new GridPos(X + 1, Y),
                new GridPos(X + 1, Y - 1),
                new GridPos(X, Y - 1),
                new GridPos(X - 1, Y - 1),
                new GridPos(X - 1, Y),
                new GridPos(X - 1, Y + 1),
            };
        }

        public int ManhattanDistance(GridPos other)
        {
            return Math.Abs(X - other.X) + Math.Abs(Y - other.Y);
        }

        public int ChebyshevDistance(GridPos other)
        {
            return Math.Max(Math.Abs(X - other.X), Math.Abs(Y - other.Y));
        }

        /// <summary>
        /// グリッド座標からワールド中心座標へ変換。tileSize = 1 の場合 (X+0.5, Y+0.5)。
        /// </summary>
        public Float2 ToWorldCenter(float tileSize = 1f)
        {
            return new Float2(X * tileSize + tileSize * 0.5f, Y * tileSize + tileSize * 0.5f);
        }

        /// <summary>
        /// ワールド座標からグリッド座標へ変換。
        /// </summary>
        public static GridPos FromWorld(Float2 worldPos, float tileSize = 1f)
        {
            return new GridPos(
                (int)MathF.Floor(worldPos.X / tileSize),
                (int)MathF.Floor(worldPos.Y / tileSize)
            );
        }

        public bool Equals(GridPos other) => X == other.X && Y == other.Y;
        public override bool Equals(object obj) => obj is GridPos other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(X, Y);
        public static bool operator ==(GridPos a, GridPos b) => a.Equals(b);
        public static bool operator !=(GridPos a, GridPos b) => !a.Equals(b);

        public override string ToString() => $"({X}, {Y})";
    }
}
