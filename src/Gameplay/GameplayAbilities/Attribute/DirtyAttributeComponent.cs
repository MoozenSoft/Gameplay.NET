// src/Gameplay/GameplayAbilities/Attribute/DirtyAttributeComponent.cs
using Friflo.Engine.ECS;

namespace Gameplay.GameplayAbilities;

/// <summary>
/// Entity 上的属性脏标记。Bit&lt;i&gt; = Attribute&lt;i&gt; 需要重算。
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
