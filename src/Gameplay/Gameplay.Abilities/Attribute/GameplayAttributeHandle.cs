using System;

namespace Gameplay.Abilities;

/// <summary>
/// GameplayAttribute 的轻量标识符——可在代码中由 SG 句柄隐式转换，也可从配置数据（int）构造。
/// 不持有读写委托，配置/序列化侧安全使用。
/// </summary>
public readonly struct GameplayAttributeHandle : IEquatable<GameplayAttributeHandle>
{
    public readonly int Id;

    public GameplayAttributeHandle(int id) => Id = id;

    public bool Equals(GameplayAttributeHandle other) => Id == other.Id;
    public override bool Equals(object? obj) => obj is GameplayAttributeHandle other && Equals(other);
    public override int GetHashCode() => Id;

    public override string ToString() => $"AttributeHandle({Id})";
}
