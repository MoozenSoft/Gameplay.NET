using System;
using Friflo.Engine.ECS;

namespace Gameplay.Abilities;

/// <summary>
/// GameplayAttribute 类型安全句柄——编译期由 SG 生成。
/// 持有全局唯一 Id + 读写委托，替代裸 int。
/// 相等性仅比较 Id（全局唯一），不比较委托。
/// </summary>
public readonly struct GameplayAttribute : IEquatable<GameplayAttribute>
{
    /// <summary>全局唯一 AttributeId（SG 分配）。</summary>
    public readonly int Id;

    private readonly Action<Entity, float> writeCurrentValue;
    private readonly TryReadValue tryReadBaseValue;
    private readonly TryReadValue tryReadCurrentValue;

    /// <summary>尝试从 Entity Component 读取值的委托签名。</summary>
    internal delegate bool TryReadValue(Entity entity, out float value);

    internal GameplayAttribute(int id, Action<Entity, float> writeCurrentValue)
    {
        Id = id;
        this.writeCurrentValue = writeCurrentValue;
        this.tryReadBaseValue = null!;
        this.tryReadCurrentValue = null!;
    }

    internal GameplayAttribute(int id,
        TryReadValue tryReadBaseValue,
        TryReadValue tryReadCurrentValue,
        Action<Entity, float> writeCurrentValue)
    {
        Id = id;
        this.tryReadBaseValue = tryReadBaseValue;
        this.tryReadCurrentValue = tryReadCurrentValue;
        this.writeCurrentValue = writeCurrentValue;
    }

    /// <summary>将评估后的 CurrentValue 写回组件字段。</summary>
    public void WriteCurrentValue(Entity entity, float value)
        => writeCurrentValue(entity, value);

    /// <summary>写回 CurrentValue。委托未初始化时静默返回 false。</summary>
    internal bool TryWriteCurrentValue(Entity entity, float value)
    {
        if (writeCurrentValue == null) return false;
        writeCurrentValue(entity, value);
        return true;
    }

    /// <summary>尝试读取 BaseValue。</summary>
    internal bool TryReadBaseValue(Entity entity, out float value)
    {
        if (tryReadBaseValue != null)
            return tryReadBaseValue(entity, out value);
        value = 0f;
        return false;
    }

    /// <summary>尝试读取 CurrentValue。</summary>
    internal bool TryReadCurrentValue(Entity entity, out float value)
    {
        if (tryReadCurrentValue != null)
            return tryReadCurrentValue(entity, out value);
        value = 0f;
        return false;
    }

    /// <summary>隐式转换为轻量标识符。</summary>
    public static implicit operator GameplayAttributeHandle(GameplayAttribute attr)
        => new(attr.Id);

    public bool Equals(GameplayAttribute other) => Id == other.Id;
    public override bool Equals(object? obj) => obj is GameplayAttribute other && Equals(other);
    public override int GetHashCode() => Id;

    public override string ToString() => $"GameplayAttribute({Id})";
}
