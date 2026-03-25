using System;

namespace FloorBreaker.Shared.Domain.Grid
{
    public enum Direction8 : byte
    {
        N,
        NE,
        E,
        SE,
        S,
        SW,
        W,
        NW,
    }

    public static class Direction8Extensions
    {
        private static readonly (int dx, int dy)[] Offsets =
        {
            (0, 1),   // N
            (1, 1),   // NE
            (1, 0),   // E
            (1, -1),  // SE
            (0, -1),  // S
            (-1, -1), // SW
            (-1, 0),  // W
            (-1, 1),  // NW
        };

        public static GridPos ToOffset(this Direction8 dir)
        {
            var (dx, dy) = Offsets[(int)dir];
            return new GridPos(dx, dy);
        }

        public static Direction8 Opposite(this Direction8 dir)
        {
            return (Direction8)(((int)dir + 4) % 8);
        }

        public static bool IsCardinal(this Direction8 dir)
        {
            return (int)dir % 2 == 0;
        }
    }
}
