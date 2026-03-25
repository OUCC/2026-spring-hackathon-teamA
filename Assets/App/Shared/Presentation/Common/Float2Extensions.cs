using UnityEngine;
using FloorBreaker.Shared.Domain.Primitives;

namespace FloorBreaker.Shared.Presentation.Common
{
    public static class Float2Extensions
    {
        public static Vector2 ToVector2(this Float2 f) => new(f.X, f.Y);
        public static Vector3 ToVector3(this Float2 f, float z = 0f) => new(f.X, f.Y, z);
        public static Float2 ToFloat2(this Vector2 v) => new(v.x, v.y);
    }
}
