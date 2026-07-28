using System;

namespace Gameplay.Abilities;

/// <summary>
/// GameplayAttribute 类型安全标识符——编译期由 SG 生成。
/// 纯 Id 包装，读写委托储存在 AttributeAggregatorManager 内部的 AttributeDescriptor 注册表。
/// </summary>
public readonly struct GameplayAttribute : IEquatable<GameplayAttribute>
{
    /// <summary>全局唯一 AttributeId（SG 分配）。</summary>
    public readonly int Id;

    public GameplayAttribute(int id) => Id = id;

    public bool Equals(GameplayAttribute other) => Id == other.Id;
    public override bool Equals(object? obj) => obj is GameplayAttribute other && Equals(other);
    public override int GetHashCode() => Id;

    public override string ToString() => $"GameplayAttribute({Id})";
}
