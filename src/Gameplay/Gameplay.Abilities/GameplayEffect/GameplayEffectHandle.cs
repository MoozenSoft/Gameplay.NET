using System;

namespace Gameplay.Abilities;

/// <summary>ActiveGameplayEffect 的轻量句柄。</summary>
/// <remarks>
/// <para>EffectSystem 内部分配。</para>
/// </remarks>
public readonly struct GameplayEffectHandle : IEquatable<GameplayEffectHandle>
{
    public readonly int Id;

    public GameplayEffectHandle(int id) => Id = id;

    public bool Equals(GameplayEffectHandle other) => Id == other.Id;
    public override bool Equals(object? obj) => obj is GameplayEffectHandle other && Equals(other);
    public override int GetHashCode() => Id;

    public bool IsValid => Id > 0;

    public static bool operator ==(GameplayEffectHandle a, GameplayEffectHandle b) => a.Id == b.Id;
    public static bool operator !=(GameplayEffectHandle a, GameplayEffectHandle b) => a.Id != b.Id;

    public override string ToString() => $"GEHandle({Id})";
}
