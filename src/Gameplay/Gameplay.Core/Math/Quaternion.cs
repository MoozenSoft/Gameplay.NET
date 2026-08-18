using System;

namespace Gameplay.Core;

/// <summary>自定义四元数（旋转，4 float）。</summary>
public readonly struct Quaternion : IEquatable<Quaternion>
{
    public readonly float X;
    public readonly float Y;
    public readonly float Z;
    public readonly float W;

    public Quaternion(float x, float y, float z, float w) { X = x; Y = y; Z = z; W = w; }

    public static Quaternion Identity => new(0f, 0f, 0f, 1f);

    public bool Equals(Quaternion other) => X == other.X && Y == other.Y && Z == other.Z && W == other.W;
    public override bool Equals(object? obj) => obj is Quaternion o && Equals(o);
    public override int GetHashCode() => HashCode.Combine(X, Y, Z, W);
    public override string ToString() => $"({X}, {Y}, {Z}, {W})";
}
