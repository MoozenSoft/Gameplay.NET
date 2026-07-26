// src/Gameplay/GameplayAbilities/Attribute/DirtyAttributeComponent.cs
using Friflo.Engine.ECS;

namespace Gameplay.Abilities;

/// <summary>
/// Entity 上的属性脏标记。DirtyBits 第 i 位为 1 表示第 i 个 Attribute 需要重算。
/// SG 编译期保证 AttributeId 不超过 64。
/// </summary>
public struct DirtyAttributeComponent : IComponent
{
    public ulong DirtyBits;

    public void SetBit(int attributeId)
        => DirtyBits |= (1UL << attributeId);

    public bool HasBit(int attributeId)
        => (DirtyBits & (1UL << attributeId)) != 0;

    public void ClearAll()
        => DirtyBits = 0UL;
}
