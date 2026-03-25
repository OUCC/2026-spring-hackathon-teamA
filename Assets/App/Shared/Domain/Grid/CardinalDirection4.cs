namespace FloorBreaker.Shared.Domain.Grid
{
    public enum CardinalDirection4 : byte
    {
        N,
        E,
        S,
        W,
    }

    public static class CardinalDirection4Extensions
    {
        private static readonly (int dx, int dy)[] Offsets =
        {
            (0, 1),  // N
            (1, 0),  // E
            (0, -1), // S
            (-1, 0), // W
        };

        public static GridPos ToOffset(this CardinalDirection4 dir)
        {
            var (dx, dy) = Offsets[(int)dir];
            return new GridPos(dx, dy);
        }

        public static CardinalDirection4 Opposite(this CardinalDirection4 dir)
        {
            return (CardinalDirection4)(((int)dir + 2) % 4);
        }

        public static Direction8 ToDirection8(this CardinalDirection4 dir)
        {
            return dir switch
            {
                CardinalDirection4.N => Direction8.N,
                CardinalDirection4.E => Direction8.E,
                CardinalDirection4.S => Direction8.S,
                CardinalDirection4.W => Direction8.W,
                _ => Direction8.N,
            };
        }
    }
}
