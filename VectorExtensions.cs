using System.Numerics;
using Silk.NET.Maths;

namespace SilkWindows;

internal static class VectorExtensions
{
    public static Vector2D<int> ToVector2DInt(this Vector2 v) => new((int)v.X, (int)v.Y);
}