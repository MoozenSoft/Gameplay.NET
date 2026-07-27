// tests/Gameplay.Tests/Gameplay.Tests.Abilities/Attribute/AttributeAggregatorManagerTests.cs
namespace Gameplay.Tests.Abilities;

using System;
using Friflo.Engine.ECS;
using Gameplay.Abilities;
using Xunit;

public class AttributeAggregatorManagerTests
{
    // 手写 GameplayAttribute（测试用，不依赖 SG）
    private static GameplayAttribute CreateTestAttribute(int id)
    {
        return new GameplayAttribute(
            id: id,
            tryReadBaseValue: (Entity entity, out float value) => {
                ref var set = ref entity.GetComponent<ManagerTestAttrSet>();
                value = set.Value.BaseValue;
                return true;
            },
            tryReadCurrentValue: (Entity entity, out float value) => {
                ref var set = ref entity.GetComponent<ManagerTestAttrSet>();
                value = set.Value.CurrentValue;
                return true;
            },
            writeCurrentValue: (entity, value) => {
                ref var data = ref entity.GetComponent<ManagerTestAttrSet>().Value;
                data.CurrentValue = value;
            });
    }

    private static (EntityStore store, Entity entity, AttributeAggregatorManager mgr,
        GameplayAttribute attr, GameplayAttributeHandle handle)
        CreateWithAttribute(int baseValue = 100)
    {
        var store = new EntityStore();
        var entity = store.CreateEntity();
        entity.AddComponent(new ManagerTestAttrSet { Value = new() { BaseValue = baseValue } });

        var attr = CreateTestAttribute(id: 200);
        var mgr = new AttributeAggregatorManager();
        mgr.RegisterAttribute(attr);
        var handle = (GameplayAttributeHandle)attr;
        return (store, entity, mgr, attr, handle);
    }

    [Fact]
    public void GetCurrentValue_NoAggregator_ReturnsBaseValue()
    {
        var (_, entity, mgr, _, handle) = CreateWithAttribute(baseValue: 100);
        Assert.Equal(100f, mgr.GetCurrentValue(entity, handle));
    }

    [Fact]
    public void SetBaseValue_CreatesAggregatorAndReadsBack()
    {
        var (_, entity, mgr, _, handle) = CreateWithAttribute(baseValue: 100);
        mgr.SetBaseValue(entity, handle, 200f);
        Assert.Equal(200f, mgr.GetBaseValue(entity, handle));
        Assert.True(mgr.HasAggregator(entity, handle));
    }

    [Fact]
    public void Flush_SingleDirty_WritesCurrentValue()
    {
        var (_, entity, mgr, _, handle) = CreateWithAttribute(baseValue: 100);
        mgr.AddAggregatorMod(entity, handle, geHandle: 1, magnitude: 20f, EGameplayModOp.Additive);
        mgr.Flush();
        Assert.Equal(120f, mgr.GetCurrentValue(entity, handle));
    }

    [Fact]
    public void Flush_MultipleChanges_OnlyEvaluatesOnce()
    {
        var (_, entity, mgr, _, handle) = CreateWithAttribute(baseValue: 100);
        mgr.AddAggregatorMod(entity, handle, 1, 20f, EGameplayModOp.Additive);
        mgr.AddAggregatorMod(entity, handle, 2, 30f, EGameplayModOp.Additive);
        mgr.Flush();
        Assert.Equal(150f, mgr.GetCurrentValue(entity, handle));
    }

    [Fact]
    public void RemoveMod_OnlyAffectsTargetAggregator()
    {
        var store = new EntityStore();
        var entity = store.CreateEntity();
        entity.AddComponent(new ManagerTestMultiAttrSet());
        var attrA = new GameplayAttribute(id: 301,
            tryReadBaseValue: (Entity e, out float v) => { ref var s = ref e.GetComponent<ManagerTestMultiAttrSet>(); v = s.FieldA.BaseValue; return true; },
            tryReadCurrentValue: (Entity e, out float v) => { ref var s = ref e.GetComponent<ManagerTestMultiAttrSet>(); v = s.FieldA.CurrentValue; return true; },
            writeCurrentValue: (e, v) => { ref var d = ref e.GetComponent<ManagerTestMultiAttrSet>().FieldA; d.CurrentValue = v; });
        var attrB = new GameplayAttribute(id: 302,
            tryReadBaseValue: (Entity e, out float v) => { ref var s = ref e.GetComponent<ManagerTestMultiAttrSet>(); v = s.FieldB.BaseValue; return true; },
            tryReadCurrentValue: (Entity e, out float v) => { ref var s = ref e.GetComponent<ManagerTestMultiAttrSet>(); v = s.FieldB.CurrentValue; return true; },
            writeCurrentValue: (e, v) => { ref var d = ref e.GetComponent<ManagerTestMultiAttrSet>().FieldB; d.CurrentValue = v; });
        var mgr = new AttributeAggregatorManager();
        mgr.RegisterAttribute(attrA);
        mgr.RegisterAttribute(attrB);

        mgr.AddAggregatorMod(entity, (GameplayAttributeHandle)attrA, 1, 20f, EGameplayModOp.Additive);
        mgr.AddAggregatorMod(entity, (GameplayAttributeHandle)attrB, 1, 50f, EGameplayModOp.Additive);

        mgr.RemoveAggregatorModsByHandle(1);

        mgr.Flush();
        Assert.Equal(0f, mgr.GetCurrentValue(entity, (GameplayAttributeHandle)attrA));
        Assert.Equal(0f, mgr.GetCurrentValue(entity, (GameplayAttributeHandle)attrB));
    }

    [Fact]
    public void RemoveMod_NonExistentHandle_NoChange()
    {
        var (_, entity, mgr, _, handle) = CreateWithAttribute(baseValue: 100);
        mgr.AddAggregatorMod(entity, handle, 1, 20f, EGameplayModOp.Additive);
        mgr.RemoveAggregatorModsByHandle(999);
        mgr.Flush();
        Assert.Equal(120f, mgr.GetCurrentValue(entity, handle));
    }

    [Fact]
    public void GetCurrentValue_BeforeFlush_ReturnsPreviousFlushedValue()
    {
        var (_, entity, mgr, _, handle) = CreateWithAttribute(baseValue: 100);
        mgr.AddAggregatorMod(entity, handle, 1, 20f, EGameplayModOp.Additive);
        float value = mgr.GetCurrentValue(entity, handle);
        Assert.Equal(0f, value);
    }

    [Fact]
    public void RemoveEntity_ClearsAggregators()
    {
        var (_, entity, mgr, _, handle) = CreateWithAttribute(baseValue: 100);
        mgr.AddAggregatorMod(entity, handle, 1, 20f, EGameplayModOp.Additive);
        Assert.True(mgr.HasAggregator(entity, handle));
        mgr.RemoveEntity(entity);
        Assert.False(mgr.HasAggregator(entity, handle));
    }

    [Fact]
    public void Flush_DirtyClearedAfterFlush()
    {
        var (_, entity, mgr, _, handle) = CreateWithAttribute(baseValue: 100);
        mgr.AddAggregatorMod(entity, handle, 1, 10f, EGameplayModOp.Additive);
        mgr.Flush();
        Assert.Equal(110f, mgr.GetCurrentValue(entity, handle));
        mgr.Flush();
        Assert.Equal(110f, mgr.GetCurrentValue(entity, handle));
    }

    [Fact]
    public void RegisterAttribute_DuplicateId_Throws()
    {
        var mgr = new AttributeAggregatorManager();
        var attr = new GameplayAttribute(id: 5, writeCurrentValue: (_, _) => { });
        mgr.RegisterAttribute(attr);
        Assert.Throws<InvalidOperationException>(() => mgr.RegisterAttribute(attr));
    }

    [Fact]
    public void MultipleAttributes_IndependentDirtyQueues()
    {
        var store = new EntityStore();
        var entity = store.CreateEntity();
        entity.AddComponent(new ManagerTestMultiAttrSet());
        var attrA = new GameplayAttribute(id: 401,
            tryReadBaseValue: (Entity e, out float v) => { ref var s = ref e.GetComponent<ManagerTestMultiAttrSet>(); v = s.FieldA.BaseValue; return true; },
            tryReadCurrentValue: (Entity e, out float v) => { ref var s = ref e.GetComponent<ManagerTestMultiAttrSet>(); v = s.FieldA.CurrentValue; return true; },
            writeCurrentValue: (e, v) => { ref var d = ref e.GetComponent<ManagerTestMultiAttrSet>().FieldA; d.CurrentValue = v; });
        var attrB = new GameplayAttribute(id: 402,
            tryReadBaseValue: (Entity e, out float v) => { ref var s = ref e.GetComponent<ManagerTestMultiAttrSet>(); v = s.FieldB.BaseValue; return true; },
            tryReadCurrentValue: (Entity e, out float v) => { ref var s = ref e.GetComponent<ManagerTestMultiAttrSet>(); v = s.FieldB.CurrentValue; return true; },
            writeCurrentValue: (e, v) => { ref var d = ref e.GetComponent<ManagerTestMultiAttrSet>().FieldB; d.CurrentValue = v; });
        var mgr = new AttributeAggregatorManager();
        mgr.RegisterAttribute(attrA);
        mgr.RegisterAttribute(attrB);

        mgr.AddAggregatorMod(entity, (GameplayAttributeHandle)attrA, 1, 20f, EGameplayModOp.Additive);
        mgr.AddAggregatorMod(entity, (GameplayAttributeHandle)attrB, 2, 50f, EGameplayModOp.Additive);
        mgr.Flush();

        Assert.Equal(20f, mgr.GetCurrentValue(entity, (GameplayAttributeHandle)attrA));
        Assert.Equal(50f, mgr.GetCurrentValue(entity, (GameplayAttributeHandle)attrB));
    }
}

// ── 本测试专用的 AttributeSet（无 [GameplayAttribute]，SG 不处理）──

internal struct ManagerTestAttrSet : IAttributeSetComponent
{
    public GameplayAttributeData Value;
}

internal struct ManagerTestMultiAttrSet : IAttributeSetComponent
{
    public GameplayAttributeData FieldA;
    public GameplayAttributeData FieldB;
}
