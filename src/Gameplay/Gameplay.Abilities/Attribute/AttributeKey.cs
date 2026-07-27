using System;
using Friflo.Engine.ECS;

namespace Gameplay.Abilities;

/// <summary>
/// Manager 内部用于索引 aggregator 的复合键。
/// 包含完整 Entity（含 Id + Revision）和 AttributeHandle，避免 Entity 删除/ID 回收后错误命中旧 aggregator。
/// </summary>
internal readonly struct AttributeKey : IEquatable<AttributeKey>
{
    internal readonly Entity Entity;
    internal readonly GameplayAttributeHandle Attribute;

    internal AttributeKey(Entity entity, GameplayAttributeHandle attribute)
    {
        Entity = entity;
        Attribute = attribute;
    }

    public bool Equals(AttributeKey other)
        => Entity == other.Entity && Attribute.Equals(other.Attribute);

    public override bool Equals(object? obj)
        => obj is AttributeKey other && Equals(other);

    public override int GetHashCode()
        => Entity.GetHashCode() ^ Attribute.GetHashCode();
}
