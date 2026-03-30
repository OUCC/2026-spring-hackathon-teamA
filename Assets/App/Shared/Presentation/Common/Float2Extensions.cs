using UnityEngine;
using FloorBreaker.Shared.Domain.Primitives;

namespace FloorBreaker.Shared.Presentation.Common
{
    public static class Float2Extensions
    {
        public static Vector3 ToVector3(this Float2 f, float z = 0f) => new(f.X, f.Y, z);
    }
}
