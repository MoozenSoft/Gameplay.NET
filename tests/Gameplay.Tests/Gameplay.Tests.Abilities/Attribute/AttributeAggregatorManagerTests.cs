// tests/Gameplay.Tests/Gameplay.Tests.Abilities/Attribute/AttributeAggregatorManagerTests.cs
namespace Gameplay.Tests.Abilities;

using Friflo.Engine.ECS;
using Gameplay.Abilities;
using Xunit;

public class AttributeAggregatorManagerTests
{
    // 注册测试用的 GameplayAttribute 描述符
    private static void RegisterTestAttribute(AttributeAggregatorManager mgr, GameplayAttribute attr)
    {
        mgr.RegisterAttribute(attr,
            (Entity entity, out float value) => {
                ref var set = ref entity.GetComponent<ManagerTestAttrSet>();
                value = set.Value.BaseValue;
            },
            (Entity entity, out float value) => {
                ref var set = ref entity.GetComponent<ManagerTestAttrSet>();
                value = set.Value.CurrentValue;
            },
            (Entity entity, float value) => {
                ref var data = ref entity.GetComponent<ManagerTestAttrSet>().Value;
                data.CurrentValue = value;
            });
    }

    private static (EntityStore store, Entity entity, GameplayAttribute attr)
        CreateWithAttribute(int baseValue = 100, int attrId = 200)
    {
        var store = new EntityStore();
        var entity = store.CreateEntity();
        entity.AddComponent(new ManagerTestAttrSet { Value = new() { BaseValue = baseValue } });
        return (store, entity, new GameplayAttribute(attrId));
    }

    [Fact]
    public void GetCurrentValue_NoAggregator_ReturnsBaseValue()
    {
        var (store, entity, attr) = CreateWithAttribute(baseValue: 100, attrId: 500);
        var mgr = new AttributeAggregatorManager();
        RegisterTestAttribute(mgr, attr);
        Assert.Equal(100f, mgr.GetCurrentValue(entity, attr));
    }

    [Fact]
    public void SetBaseValue_CreatesAggregatorAndReadsBack()
    {
        var (store, entity, attr) = CreateWithAttribute(baseValue: 100, attrId: 501);
        var mgr = new AttributeAggregatorManager();
        RegisterTestAttribute(mgr, attr);
        mgr.SetBaseValue(entity, attr, 200f);
        Assert.Equal(200f, mgr.GetBaseValue(entity, attr));
        Assert.True(mgr.HasAggregator(entity, attr));
    }

    [Fact]
    public void Flush_SingleDirty_WritesCurrentValue()
    {
        var (store, entity, attr) = CreateWithAttribute(baseValue: 100, attrId: 502);
        var mgr = new AttributeAggregatorManager();
        RegisterTestAttribute(mgr, attr);
        mgr.AddAggregatorMod(entity, attr, geHandle: new GameplayEffectHandle(1), magnitude: 20f, EGameplayModOp.Additive);
        mgr.Flush();
        Assert.Equal(120f, mgr.GetCurrentValue(entity, attr));
    }

    [Fact]
    public void Flush_MultipleChanges_OnlyEvaluatesOnce()
    {
        var (store, entity, attr) = CreateWithAttribute(baseValue: 100, attrId: 503);
        var mgr = new AttributeAggregatorManager();
        RegisterTestAttribute(mgr, attr);
        mgr.AddAggregatorMod(entity, attr, new GameplayEffectHandle(1), 20f, EGameplayModOp.Additive);
        mgr.AddAggregatorMod(entity, attr, new GameplayEffectHandle(2), 30f, EGameplayModOp.Additive);
        mgr.Flush();
        Assert.Equal(150f, mgr.GetCurrentValue(entity, attr));
    }

    [Fact]
    public void RemoveMod_OnlyAffectsTargetAggregator()
    {
        var store = new EntityStore();
        var entity = store.CreateEntity();
        entity.AddComponent(new ManagerTestMultiAttrSet());
        var mgr = new AttributeAggregatorManager();
        mgr.RegisterAttribute(new GameplayAttribute(301),
            (Entity e, out float v) => { ref var s = ref e.GetComponent<ManagerTestMultiAttrSet>(); v = s.FieldA.BaseValue; },
            (Entity e, out float v) => { ref var s = ref e.GetComponent<ManagerTestMultiAttrSet>(); v = s.FieldA.CurrentValue; },
            (Entity e, float v) => { e.GetComponent<ManagerTestMultiAttrSet>().FieldA.CurrentValue = v; });
        mgr.RegisterAttribute(new GameplayAttribute(302),
            (Entity e, out float v) => { ref var s = ref e.GetComponent<ManagerTestMultiAttrSet>(); v = s.FieldB.BaseValue; },
            (Entity e, out float v) => { ref var s = ref e.GetComponent<ManagerTestMultiAttrSet>(); v = s.FieldB.CurrentValue; },
            (Entity e, float v) => { e.GetComponent<ManagerTestMultiAttrSet>().FieldB.CurrentValue = v; });

        mgr.AddAggregatorMod(entity, new GameplayAttribute(301), new GameplayEffectHandle(1), 20f, EGameplayModOp.Additive);
        mgr.AddAggregatorMod(entity, new GameplayAttribute(302), new GameplayEffectHandle(1), 50f, EGameplayModOp.Additive);
        mgr.RemoveAggregatorModsByHandle(new GameplayEffectHandle(1));
        mgr.Flush();
        Assert.Equal(0f, mgr.GetCurrentValue(entity, new GameplayAttribute(301)));
        Assert.Equal(0f, mgr.GetCurrentValue(entity, new GameplayAttribute(302)));
    }

    [Fact]
    public void RemoveMod_NonExistentHandle_NoChange()
    {
        var (store, entity, attr) = CreateWithAttribute(baseValue: 100, attrId: 504);
        var mgr = new AttributeAggregatorManager();
        RegisterTestAttribute(mgr, attr);
        mgr.AddAggregatorMod(entity, attr, new GameplayEffectHandle(1), 20f, EGameplayModOp.Additive);
        mgr.RemoveAggregatorModsByHandle(new GameplayEffectHandle(999));
        mgr.Flush();
        Assert.Equal(120f, mgr.GetCurrentValue(entity, attr));
    }

    [Fact]
    public void GetCurrentValue_BeforeFlush_ReturnsPreviousFlushedValue()
    {
        var (store, entity, attr) = CreateWithAttribute(baseValue: 100, attrId: 505);
        var mgr = new AttributeAggregatorManager();
        RegisterTestAttribute(mgr, attr);
        mgr.AddAggregatorMod(entity, attr, new GameplayEffectHandle(1), 20f, EGameplayModOp.Additive);
        float value = mgr.GetCurrentValue(entity, attr);
        Assert.Equal(0f, value);
    }

    [Fact]
    public void RemoveEntity_ClearsAggregators()
    {
        var (store, entity, attr) = CreateWithAttribute(baseValue: 100, attrId: 506);
        var mgr = new AttributeAggregatorManager();
        RegisterTestAttribute(mgr, attr);
        mgr.AddAggregatorMod(entity, attr, new GameplayEffectHandle(1), 20f, EGameplayModOp.Additive);
        Assert.True(mgr.HasAggregator(entity, attr));
        mgr.RemoveEntity(entity);
        Assert.False(mgr.HasAggregator(entity, attr));
    }

    [Fact]
    public void Flush_DirtyClearedAfterFlush()
    {
        var (store, entity, attr) = CreateWithAttribute(baseValue: 100, attrId: 507);
        var mgr = new AttributeAggregatorManager();
        RegisterTestAttribute(mgr, attr);
        mgr.AddAggregatorMod(entity, attr, new GameplayEffectHandle(1), 10f, EGameplayModOp.Additive);
        mgr.Flush();
        Assert.Equal(110f, mgr.GetCurrentValue(entity, attr));
        mgr.Flush();
        Assert.Equal(110f, mgr.GetCurrentValue(entity, attr));
    }

    [Fact]
    public void RegisterAttribute_CoveredSilently()
    {
        var mgr = new AttributeAggregatorManager();
        mgr.RegisterAttribute(new GameplayAttribute(5),
            (Entity _, out float v) => { v = 0; },
            (Entity _, out float v) => { v = 0; },
            (Entity _, float _) => { });
        // 覆盖不抛异常
        mgr.RegisterAttribute(new GameplayAttribute(5),
            (Entity _, out float v) => { v = 1; },
            (Entity _, out float v) => { v = 1; },
            (Entity _, float _) => { });
    }

    [Fact]
    public void MultipleAttributes_IndependentDirtyQueues()
    {
        var store = new EntityStore();
        var entity = store.CreateEntity();
        entity.AddComponent(new ManagerTestMultiAttrSet());
        var mgr = new AttributeAggregatorManager();
        mgr.RegisterAttribute(new GameplayAttribute(401),
            (Entity e, out float v) => { ref var s = ref e.GetComponent<ManagerTestMultiAttrSet>(); v = s.FieldA.BaseValue; },
            (Entity e, out float v) => { ref var s = ref e.GetComponent<ManagerTestMultiAttrSet>(); v = s.FieldA.CurrentValue; },
            (Entity e, float v) => { e.GetComponent<ManagerTestMultiAttrSet>().FieldA.CurrentValue = v; });
        mgr.RegisterAttribute(new GameplayAttribute(402),
            (Entity e, out float v) => { ref var s = ref e.GetComponent<ManagerTestMultiAttrSet>(); v = s.FieldB.BaseValue; },
            (Entity e, out float v) => { ref var s = ref e.GetComponent<ManagerTestMultiAttrSet>(); v = s.FieldB.CurrentValue; },
            (Entity e, float v) => { e.GetComponent<ManagerTestMultiAttrSet>().FieldB.CurrentValue = v; });

        mgr.AddAggregatorMod(entity, new GameplayAttribute(401), new GameplayEffectHandle(1), 20f, EGameplayModOp.Additive);
        mgr.AddAggregatorMod(entity, new GameplayAttribute(402), new GameplayEffectHandle(2), 50f, EGameplayModOp.Additive);
        mgr.Flush();
        Assert.Equal(20f, mgr.GetCurrentValue(entity, new GameplayAttribute(401)));
        Assert.Equal(50f, mgr.GetCurrentValue(entity, new GameplayAttribute(402)));
    }
}

internal struct ManagerTestAttrSet : IAttributeSetComponent
{
    public GameplayAttributeData Value;
}

internal struct ManagerTestMultiAttrSet : IAttributeSetComponent
{
    public GameplayAttributeData FieldA;
    public GameplayAttributeData FieldB;
}
