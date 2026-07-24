// tests/Gameplay.Tests/Gameplay.Tests.Abilities/Attribute/CurrentValueWritebackTests.cs
namespace Gameplay.Tests.Abilities;

using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Gameplay.Abilities;
using Xunit;

public class CurrentValueWritebackTests
{
    [Fact]
    public void OnUpdate_EvaluatesAndWritesBackCurrentValue()
    {
        var store = new EntityStore();
        var attrSys = new AttributeSystem();
        var root = new SystemRoot(store) { attrSys };

        // 注册写回委托 — 使用 SG 生成的 GetHealth 访问器
        attrSys.RegisterCurrentValueWriter(0, (entity, value) =>
        {
            ref var health = ref TestAttrSet.GetHealth(entity);
            health.CurrentValue = value;
        });

        var entity = store.CreateEntity();
        entity.AddComponent(new DirtyAttributeComponent());
        entity.AddComponent(new TestAttrSet { Health = new() { BaseValue = 100f } });

        attrSys.SetAggregatorValue(entity, 0, 100f);
        attrSys.AddAggregatorMod(entity, 0, handle: 1, magnitude: 20f, EGameplayModOp.Additive);
        ref var dirty = ref entity.GetComponent<DirtyAttributeComponent>();
        dirty.SetBit(0);

        root.Update(new UpdateTick(0f, 0));

        ref var healthData = ref TestAttrSet.GetHealth(entity);
        Assert.Equal(120f, healthData.CurrentValue, 0.001f); // 100 + 20
        Assert.Equal(100f, healthData.BaseValue);             // Base 不变
    }

    [Fact]
    public void OnUpdate_NoWriterRegistered_DoesNotThrow()
    {
        var store = new EntityStore();
        var attrSys = new AttributeSystem();
        var root = new SystemRoot(store) { attrSys };

        var entity = store.CreateEntity();
        entity.AddComponent(new DirtyAttributeComponent());
        attrSys.SetAggregatorValue(entity, 0, 50f);
        attrSys.AddAggregatorMod(entity, 0, handle: 1, magnitude: 10f, EGameplayModOp.Additive);
        ref var dirty = ref entity.GetComponent<DirtyAttributeComponent>();
        dirty.SetBit(0);

        // 未注册 writer → 不抛异常，静默跳过
        root.Update(new UpdateTick(0f, 0));
    }
}
