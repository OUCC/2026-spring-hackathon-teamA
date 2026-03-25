using System;

namespace FloorBreaker.Shared.Domain.Primitives
{
    public readonly struct Float2 : IEquatable<Float2>
    {
        public readonly float X;
        public readonly float Y;

        public Float2(float x, float y)
        {
            X = x;
            Y = y;
        }

        public static Float2 Zero => new(0f, 0f);
        public static Float2 One => new(1f, 1f);

        public static Float2 operator +(Float2 a, Float2 b) => new(a.X + b.X, a.Y + b.Y);
        public static Float2 operator -(Float2 a, Float2 b) => new(a.X - b.X, a.Y - b.Y);
        public static Float2 operator *(Float2 a, float s) => new(a.X * s, a.Y * s);
        public static Float2 operator *(float s, Float2 a) => new(a.X * s, a.Y * s);

        public bool Equals(Float2 other) => X.Equals(other.X) && Y.Equals(other.Y);
        public override bool Equals(object obj) => obj is Float2 other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(X, Y);
        public static bool operator ==(Float2 a, Float2 b) => a.Equals(b);
        public static bool operator !=(Float2 a, Float2 b) => !a.Equals(b);

        public override string ToString() => $"({X:F2}, {Y:F2})";
    }
}
