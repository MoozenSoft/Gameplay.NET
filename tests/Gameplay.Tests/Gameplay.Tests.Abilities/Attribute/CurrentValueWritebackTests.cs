// tests/Gameplay.Tests/Gameplay.Tests.Abilities/Attribute/CurrentValueWritebackTests.cs
namespace Gameplay.Tests.Abilities;

using System;
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

        // SG 生成的 RegisterAll：批量注册 GameplayAttribute 句柄
        TestAttrSetAttributes.RegisterAll(mgr);
        var healthHandle = (GameplayAttributeHandle)TestAttrSetAttributes.Health;

        var entity = store.CreateEntity();
        entity.AddComponent(new TestAttrSet { Health = new() { BaseValue = 100f } });

        mgr.AddAggregatorMod(entity, healthHandle, geHandle: 1, magnitude: 20f, op: EGameplayModOp.Additive);

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

        // 使用手动创建的 GameplayAttribute（仅写回委托），不注册——Manager 容错
        var unregisteredAttr = new GameplayAttribute(
            id: 99,
            writeCurrentValue: (entity, value) => { }
        );
        var handle = (GameplayAttributeHandle)unregisteredAttr;

        var entity = store.CreateEntity();

        // 未注册的 Attribute 仍可创建 Aggregator 并加 Mod——只是写不回 Component
        mgr.AddAggregatorMod(entity, handle, geHandle: 1, magnitude: 10f, EGameplayModOp.Additive);
        mgr.Flush();
        Assert.Equal(10f, mgr.GetCurrentValue(entity, 99));
    }
}
