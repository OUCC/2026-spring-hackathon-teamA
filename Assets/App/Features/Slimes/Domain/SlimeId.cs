using System;

namespace FloorBreaker.Slimes.Domain
{
    public readonly struct SlimeId : IEquatable<SlimeId>
    {
        public readonly int Value;

        public SlimeId(int value) => Value = value;

        public bool Equals(SlimeId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is SlimeId other && Equals(other);
        public override int GetHashCode() => Value;
        public override string ToString() => $"Slime({Value})";

        public static bool operator ==(SlimeId a, SlimeId b) => a.Value == b.Value;
        public static bool operator !=(SlimeId a, SlimeId b) => a.Value != b.Value;
    }
}
