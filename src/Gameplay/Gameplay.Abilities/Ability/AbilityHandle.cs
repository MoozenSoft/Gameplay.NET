using System;

namespace Gameplay.Abilities;

/// <summary>ActiveAbility 的轻量句柄。</summary>
/// <remarks>
/// <para>AbilityActivationManager 内部分配。</para>
/// </remarks>
public readonly struct AbilityHandle : IEquatable<AbilityHandle>
{
    public readonly int Id;

    public AbilityHandle(int id) => Id = id;

    public bool IsValid => Id > 0;

    public bool Equals(AbilityHandle other) => Id == other.Id;
    public override bool Equals(object? obj) => obj is AbilityHandle other && Equals(other);
    public override int GetHashCode() => Id;

    public static bool operator ==(AbilityHandle a, AbilityHandle b) => a.Id == b.Id;
    public static bool operator !=(AbilityHandle a, AbilityHandle b) => a.Id != b.Id;

    public override string ToString() => $"AbilityHandle({Id})";
}
