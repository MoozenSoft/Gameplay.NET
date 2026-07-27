using Friflo.Engine.ECS;

namespace Gameplay.Abilities;

/// <summary>
/// 直接消耗属性值的 Commit（Mana/Stamina/Ammo 等）。
/// 一次性状态变化不包装 Instant GE。
/// </summary>
public class ConsumeCostCommit : IAbilityCommit
{
    private readonly AttributeAggregatorManager mgr;
    private readonly int attributeId;
    private readonly float amount; // 正数 = 消耗

    public ConsumeCostCommit(AttributeAggregatorManager mgr, int attributeId, float cost)
    {
        this.mgr = mgr;
        this.attributeId = attributeId;
        amount = cost;
    }

    public void Execute(Entity owner, AbilitySpec spec, in AbilityActivationRequest request)
    {
        float current = mgr.GetCurrentValue(owner, attributeId);
        float newValue = current - amount;
        if (newValue < 0) newValue = 0;
        mgr.SetAggregatorValue(owner, attributeId, newValue);
    }
}
