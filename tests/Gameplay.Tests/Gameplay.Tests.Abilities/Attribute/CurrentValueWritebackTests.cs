// tests/Gameplay.Tests/Gameplay.Tests.Abilities/Attribute/CurrentValueWritebackTests.cs
namespace Gameplay.Tests.Abilities;

using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Gameplay.Abilities;
using Xunit;

public class CurrentValueWritebackTests
{
    [Fact]
    public void OnUpdate_SGHandles_EvaluatesAndWritesBackCurrentValue()
    {
        var store = new EntityStore();
        var attrSys = new AttributeSystem();
        var root = new SystemRoot(store) { attrSys };

        // SG 生成的 RegisterAll：批量注册 GameplayAttribute 句柄
        TestAttrSetAttributes.RegisterAll(attrSys);
        int healthId = TestAttrSetAttributes.Health.Id;

        var entity = store.CreateEntity();
        entity.AddComponent(new DirtyAttributeComponent());
        entity.AddComponent(new TestAttrSet { Health = new() { BaseValue = 100f } });

        attrSys.SetAggregatorValue(entity, healthId, 100f);
        attrSys.AddAggregatorMod(entity, healthId, handle: 1, magnitude: 20f, EGameplayModOp.Additive);
        ref var dirty = ref entity.GetComponent<DirtyAttributeComponent>();
        dirty.SetBit(healthId);

        root.Update(new UpdateTick(0f, 0));

        ref var healthData = ref TestAttrSet.GetHealth(entity);
        Assert.Equal(120f, healthData.CurrentValue, 0.001f);
        Assert.Equal(100f, healthData.BaseValue);
    }

    [Fact]
    public void OnUpdate_NoRegistration_DoesNotThrow()
    {
        var store = new EntityStore();
        var attrSys = new AttributeSystem();
        var root = new SystemRoot(store) { attrSys };

        int attrId = 99; // 故意用不存在的 ID——不注册 writer
        var entity = store.CreateEntity();
        entity.AddComponent(new DirtyAttributeComponent());
        attrSys.SetAggregatorValue(entity, attrId, 50f);
        attrSys.AddAggregatorMod(entity, attrId, handle: 1, magnitude: 10f, EGameplayModOp.Additive);
        ref var dirty = ref entity.GetComponent<DirtyAttributeComponent>();
        dirty.SetBit(attrId);

        root.Update(new UpdateTick(0f, 0));
    }
}
