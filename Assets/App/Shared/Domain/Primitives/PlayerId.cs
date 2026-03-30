using System;

namespace FloorBreaker.Shared.Domain.Primitives
{
    public readonly struct PlayerId : IEquatable<PlayerId>
    {
        public static readonly PlayerId Player1 = new(0);
        public static readonly PlayerId Player2 = new(1);

        private readonly byte _value;

        internal PlayerId(byte value) => _value = value;

        public static PlayerId FromIndex(int index) => new((byte)index);

        public int Index => _value;

        public bool Equals(PlayerId other) => _value == other._value;
        public override bool Equals(object obj) => obj is PlayerId other && Equals(other);
        public override int GetHashCode() => _value;
        public static bool operator ==(PlayerId a, PlayerId b) => a.Equals(b);
        public static bool operator !=(PlayerId a, PlayerId b) => !a.Equals(b);

        public override string ToString() => $"P{_value + 1}";
    }
}
