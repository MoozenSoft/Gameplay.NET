// tests/Gameplay.Tests/Gameplay.Tests.Abilities/Attribute/CurrentValueWritebackTests.cs
namespace Gameplay.Tests.Abilities;

using Friflo.Engine.ECS;
using Gameplay.Abilities;
using Xunit;

public class CurrentValueWritebackTests
{
    [Fact]
    public void Flush_SGHandles_EvaluatesAndWritesBackCurrentValue()
    {
        var store = new EntityStore();
        var mgr = new AttributeAggregatorManager();

        // SG 生成的 RegisterAll：批量注册 AttributeDescriptor
        TestAttrSetAttributes.RegisterAll(mgr);
        var health = TestAttrSetAttributes.Health;

        var entity = store.CreateEntity();
        entity.AddComponent(new TestAttrSet { Health = new() { BaseValue = 100f } });

        mgr.AddAggregatorMod(entity, health, geHandle: new GameplayEffectHandle(1), magnitude: 20f, EGameplayModOp.Additive);
        mgr.Flush();

        ref var healthData = ref TestAttrSet.GetHealth(entity);
        Assert.Equal(120f, healthData.CurrentValue, 0.001f);
        Assert.Equal(100f, healthData.BaseValue);
    }

    [Fact]
    public void Flush_NoRegistration_StillWorks()
    {
        var store = new EntityStore();
        var mgr = new AttributeAggregatorManager();

        var unregistered = new GameplayAttribute(99);
        var entity = store.CreateEntity();

        // 未注册的 Attribute 仍可创建 Aggregator 并加 Mod——只是写不回 Component
        mgr.AddAggregatorMod(entity, unregistered, geHandle: new GameplayEffectHandle(1), magnitude: 10f, EGameplayModOp.Additive);
        mgr.Flush();
        Assert.Equal(10f, mgr.GetCurrentValue(entity, unregistered));
    }
}
