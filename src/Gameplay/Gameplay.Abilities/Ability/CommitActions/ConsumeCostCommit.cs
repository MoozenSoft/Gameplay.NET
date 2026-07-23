using Friflo.Engine.ECS;

namespace Gameplay.Abilities;

/// <summary>
/// 直接消耗属性值的 Commit（Mana/Stamina/Ammo 等）。
/// 一次性状态变化不包装 Instant GE。
/// </summary>
public class ConsumeCostCommit : IAbilityCommit
{
    private readonly AttributeSystem attributeSystem;
    private readonly int attributeId;
    private readonly float amount; // 正数 = 消耗

    /// <param name="attr">要消耗的 GameplayAttribute 句柄（当前用 attributeId）。</param>
    /// <param name="cost">消耗量（正数）。</param>
    public ConsumeCostCommit(AttributeSystem attrSys, int attributeId, float cost)
    {
        attributeSystem = attrSys;
        this.attributeId = attributeId;
        amount = cost;
    }

    public void Execute(Entity owner, AbilitySpec spec, in AbilityActivationRequest request)
    {
        // 直接读 CurrentValue → 减去 → 写回 → 标记 Dirty
        float current = attributeSystem.GetCurrentValue(owner, attributeId);
        attributeSystem.SetAggregatorValue(owner, attributeId, current - amount);

        if (owner.TryGetComponent<DirtyAttributeComponent>(out var dirty))
            dirty.SetBit(attributeId);
    }
}
