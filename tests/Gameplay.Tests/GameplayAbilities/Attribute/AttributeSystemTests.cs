// tests/Gameplay.Tests/GameplayAbilities/Attribute/AttributeSystemTests.cs
namespace Gameplay.Tests.GameplayAbilities;

using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Gameplay.GameplayAbilities;
using Xunit;

public class AttributeSystemTests
{
    [Fact]
    public void Tick_SingleDirtyBit_EvaluatesAndClears()
    {
        var store = new EntityStore();
        var sys = new AttributeSystem();
        var root = new SystemRoot(store) { sys };

        var entity = store.CreateEntity();
        entity.AddComponent(new DirtyAttributeComponent());
        // 模拟 Apply 后的 state：Aggregator 有 Mod，DirtyBit 设置
        sys.SetAggregatorValue(entity, attributeId: 3, baseValue: 100f);
        sys.AddAggregatorMod(entity, 3, handle: 1, magnitude: 20f, EGameplayModOp.Additive);

        ref var dirty = ref entity.GetComponent<DirtyAttributeComponent>();
        dirty.SetBit(3);

        root.Update(default); // Trigger AttributeSystem.OnUpdate

        // 重算后 DirtyBit 清零
        ref var finalDirty = ref entity.GetComponent<DirtyAttributeComponent>();
        Assert.False(finalDirty.HasBit(3));
    }

    [Fact]
    public void RemoveEntity_CleansUpAggregator()
    {
        var store = new EntityStore();
        var sys = new AttributeSystem();
        var root = new SystemRoot(store) { sys };

        var entity = store.CreateEntity();
        sys.SetAggregatorValue(entity, attributeId: 0, baseValue: 50f);
        sys.AddAggregatorMod(entity, 0, handle: 1, magnitude: 10f, EGameplayModOp.Additive);
        sys.AddAggregatorMod(entity, 0, handle: 2, magnitude: 5f, EGameplayModOp.Additive);

        // Get value before removal
        float valBefore = sys.GetCurrentValue(entity, attributeId: 0);
        Assert.Equal(65f, valBefore); // 50 + 10 + 5

        // Remove handle 2
        sys.RemoveAggregatorModsByHandle(2);

        float valAfter = sys.GetCurrentValue(entity, attributeId: 0);
        Assert.Equal(60f, valAfter); // 50 + 10 only
    }
}
