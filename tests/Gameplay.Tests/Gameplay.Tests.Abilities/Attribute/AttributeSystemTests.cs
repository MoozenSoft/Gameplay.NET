// tests/Gameplay.Tests/GameplayAbilities/Attribute/AttributeSystemTests.cs
namespace Gameplay.Tests.Abilities;

using Friflo.Engine.ECS;
using Gameplay.Abilities;
using Xunit;

public class AttributeSystemTests
{
    [Fact]
    public void Tick_SingleDirtyBit_EvaluatesAndClears()
    {
        var store = new EntityStore();
        var mgr = new AttributeAggregatorManager();

        var entity = store.CreateEntity();
        // 模拟 Apply 后的 state：Aggregator 有 Mod
        mgr.SetAggregatorValue(entity, attributeId: 3, 100f);
        mgr.AddAggregatorMod(entity, attributeId: 3, geHandle: 1, magnitude: 20f, EGameplayModOp.Additive);

        mgr.Flush();

        // 重算后 CurrentValue = 100 + 20 = 120
        Assert.Equal(120f, mgr.GetCurrentValue(entity, attributeId: 3));
    }

    [Fact]
    public void RemoveEntity_CleansUpAggregator()
    {
        var store = new EntityStore();
        var mgr = new AttributeAggregatorManager();

        var entity = store.CreateEntity();
        mgr.SetAggregatorValue(entity, attributeId: 0, 50f);
        mgr.AddAggregatorMod(entity, attributeId: 0, geHandle: 1, magnitude: 10f, EGameplayModOp.Additive);
        mgr.AddAggregatorMod(entity, attributeId: 0, geHandle: 2, magnitude: 5f, EGameplayModOp.Additive);

        // Get value before removal
        mgr.Flush();
        float valBefore = mgr.GetCurrentValue(entity, attributeId: 0);
        Assert.Equal(65f, valBefore); // 50 + 10 + 5

        // Remove handle 2
        mgr.RemoveAggregatorModsByHandle(2);

        mgr.Flush();
        float valAfter = mgr.GetCurrentValue(entity, attributeId: 0);
        Assert.Equal(60f, valAfter); // 50 + 10 only
    }
}
