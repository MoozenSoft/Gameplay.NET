using System;

namespace Gameplay;

/// <summary>不可变的轻量 GameplayTag 句柄，包装一个 int id。</summary>
public readonly struct GameplayTag : IEquatable<GameplayTag>
{
    internal readonly int id;

    internal GameplayTag(int id) => this.id = id;

    /// <summary>id 为 0 表示无效 Tag。</summary>
    public static GameplayTag Invalid => default;

    public bool IsValid => id > 0;

    /// <summary>从已注册集合获取 GameplayTag。不存在则返回 Invalid。</summary>
    public static GameplayTag Request(string tagName)
        => GameplayTagManager.RequestTag(tagName);

    /// <summary>层级匹配：此 Tag 是否是 parent 自身或其子孙。</summary>
    public bool Matches(GameplayTag parent)
        => GameplayTagManager.Matches(id, parent.id);

    /// <summary>精确匹配（仅检查 id 相等）。</summary>
    public bool MatchesExact(GameplayTag other) => id == other.id;

    internal ReadOnlySpan<long> GetExpandedSet()
        => GameplayTagManager.GetExpandedSet(id);

    public bool Equals(GameplayTag other) => id == other.id;
    public override bool Equals(object? obj) => obj is GameplayTag other && Equals(other);
    public override int GetHashCode() => id;

    public override string ToString()
        => IsValid ? GameplayTagManager.GetName(id)! : "Invalid";
}
