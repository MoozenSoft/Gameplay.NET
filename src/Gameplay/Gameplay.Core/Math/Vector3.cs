using System;

namespace Gameplay.Core;

/// <summary>自定义 3D 向量（跨 TFM 稳定、序列化友好）。</summary>
public readonly struct Vector3 : IEquatable<Vector3>
{
    public readonly float X;
    public readonly float Y;
    public readonly float Z;

    public Vector3(float x, float y, float z) { X = x; Y = y; Z = z; }

    public static Vector3 Zero => default;

    public static Vector3 operator +(in Vector3 a, in Vector3 b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
    public static Vector3 operator -(in Vector3 a, in Vector3 b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
    public static Vector3 operator *(in Vector3 a, float s) => new(a.X * s, a.Y * s, a.Z * s);

    public static float Dot(in Vector3 a, in Vector3 b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;

    public float LengthSquared() => X * X + Y * Y + Z * Z;

    public Vector3 Normalized()
    {
        var len = (float)Math.Sqrt(LengthSquared());
        return len <= 0f ? Zero : this * (1f / len);
    }

    public bool Equals(Vector3 other) => X == other.X && Y == other.Y && Z == other.Z;
    public override bool Equals(object? obj) => obj is Vector3 o && Equals(o);
    public override int GetHashCode() => HashCode.Combine(X, Y, Z);
    public override string ToString() => $"({X}, {Y}, {Z})";
}
