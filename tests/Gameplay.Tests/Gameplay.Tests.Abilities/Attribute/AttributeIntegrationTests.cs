// tests/Gameplay.Tests/Gameplay.Tests.Abilities/Attribute/AttributeIntegrationTests.cs
namespace Gameplay.Tests.Abilities;

using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Gameplay.Abilities;
using Xunit;

/// <summary>
/// 端到端集成测试：验证 SG Attribute → GE Modifier → EffectSystem.Apply → AttributeSystem 完整链路。
/// </summary>
public class AttributeIntegrationTests
{
    [Fact]
    public void ApplyGE_WithScalableFloatModifier_AggregatorUpdates()
    {
        // Arrange
        var store = new EntityStore();
        var mgr = new AttributeAggregatorManager();
        var effectSys = new EffectSystem(mgr);

        var target = store.CreateEntity();
        target.AddComponent(new TestAttrSet());

        // Step 1: 使用 SG 生成的 GetHealth 访问器设置初始 BaseValue
        ref var healthData = ref TestAttrSet.GetHealth(target);
        healthData.BaseValue = 100f;

        // Step 2: 创建 GameplayEffect —— Modifier 指向 Attribute =0 (Health)
        var ge = new GameplayEffect
        {
            DurationPolicy = EGameplayEffectDurationType.Infinite,
        };
        ge.Modifiers.Add(new GameplayModifier
        {
            Attribute = new GameplayAttributeHandle(0),
            ModOp = EGameplayModOp.Additive,
            MagnitudeCalc = GameplayEffectModifierMagnitude.CreateScalableFloat(1.0f, 20f),
        });

        // Step 3: 创建 Spec，填入 Evaluate 后的 Magnitude
        // (正常流程中 Spec 构建阶段将 GameplayModifier.MagnitudeCalc 计算为 FModifierSpec.EvaluatedMagnitude)
        var spec = new GameplayEffectSpec(ge, 1f);
        spec.Modifiers.Add(new FModifierSpec
        {
            Attribute = new GameplayAttributeHandle(0),
            ModOp = EGameplayModOp.Additive,
            EvaluatedMagnitude = 20f, // ScalableFloat: coeff * level + value = 1*1 + 20 = 21? 简化用 20
            CapturePolicy = EAttributeCapturePolicy.Snapshot,
        });

        // Act
        int handle = effectSys.Apply(spec, target);

        // Assert
        Assert.True(handle > 0, "Apply should return a valid handle");

        // 验证 Aggregator CurrentValue 包含 Modifier 的效果
        // (EffectSystem 默认设置 BaseValue=0，故结果 = 0 + 20 = 20)
        float after = mgr.GetCurrentValue(target, 0);
        Assert.Equal(20f, after, 0.001f);

        // 验证 SG accessor：Component 的 BaseValue 未被修改（Aggregator 独立存储）
        ref var finalHealth = ref TestAttrSet.GetHealth(target);
        Assert.Equal(100f, finalHealth.BaseValue, 0.001f);
        Assert.Equal(0f, finalHealth.CurrentValue, 0.001f);
    }

    [Fact]
    public void ApplyGE_MultipleAttributes_EachAggregatorUpdatesIndependently()
    {
        // Arrange
        var store = new EntityStore();
        var mgr = new AttributeAggregatorManager();
        var effectSys = new EffectSystem(mgr);

        var target = store.CreateEntity();
        target.AddComponent(new MultiFieldAttrSet());

        var ge = new GameplayEffect
        {
            DurationPolicy = EGameplayEffectDurationType.Infinite,
        };
        ge.Modifiers.Add(new GameplayModifier { Attribute = new GameplayAttributeHandle(0), ModOp = EGameplayModOp.Additive });
        ge.Modifiers.Add(new GameplayModifier { Attribute = new GameplayAttributeHandle(1), ModOp = EGameplayModOp.Additive });

        var spec = new GameplayEffectSpec(ge, 1f);
        spec.Modifiers.Add(new FModifierSpec { Attribute = new GameplayAttributeHandle(0), ModOp = EGameplayModOp.Additive, EvaluatedMagnitude = 10f });
        spec.Modifiers.Add(new FModifierSpec { Attribute = new GameplayAttributeHandle(1), ModOp = EGameplayModOp.Additive, EvaluatedMagnitude = 20f });

        // Act
        int handle = effectSys.Apply(spec, target);

        // Assert
        Assert.True(handle > 0);
        Assert.Equal(10f, mgr.GetCurrentValue(target, 0), 0.001f);
        Assert.Equal(20f, mgr.GetCurrentValue(target, 1), 0.001f);

        // SG accessor: 组件数据未被修改
        ref var str = ref MultiFieldAttrSet.GetStrength(target);
        ref var agi = ref MultiFieldAttrSet.GetAgility(target);
        Assert.Equal(0f, str.BaseValue, 0.001f);
        Assert.Equal(0f, agi.BaseValue, 0.001f);
    }

    [Fact]
    public void ApplyGE_ThenFlush_EvaluatesAndWritesBack()
    {
        // 验证 AttributeAggregatorManager Flush 完整流程。
        var store = new EntityStore();
        var mgr = new AttributeAggregatorManager();
        var effectSys = new EffectSystem(mgr);

        var target = store.CreateEntity();
        target.AddComponent(new TestAttrSet());

        // 1. Apply GE — 设置 Aggregator（内部 MarkDirty）
        var spec = new GameplayEffectSpec(new GameplayEffect(), 1f);
        spec.Modifiers.Add(new FModifierSpec
        {
            Attribute = new GameplayAttributeHandle(0),
            ModOp = EGameplayModOp.Additive,
            EvaluatedMagnitude = 30f,
        });
        int handle = effectSys.Apply(spec, target);
        Assert.True(handle > 0);

        // 2. Flush → Evaluate + WriteBack
        mgr.Flush();

        // 3. Aggregator 值正确
        Assert.Equal(30f, mgr.GetCurrentValue(target, 0), 0.001f);
    }

    [Fact]
    public void FullChain_SGAccessor_GE_Apply_Verify()
    {
        // 最简完整链路：SG → GE Spec → Apply → Aggregator → 验证
        var store = new EntityStore();
        var mgr = new AttributeAggregatorManager();
        var effectSys = new EffectSystem(mgr);

        var target = store.CreateEntity();
        target.AddComponent(new TestAttrSet());

        // 1. SG accessor: 写 BaseValue
        TestAttrSet.GetHealth(target).BaseValue = 100f;

        // 2. 构建 GameplayEffectSpec，Modifier 指向 AttributeId=0 (Health)
        var spec = new GameplayEffectSpec(new GameplayEffect(), 1f);
        spec.Modifiers.Add(new FModifierSpec
        {
            Attribute = new GameplayAttributeHandle(0),
            ModOp = EGameplayModOp.Additive,
            EvaluatedMagnitude = 25f,
        });

        // 3. Apply
        effectSys.Apply(spec, target);

        // 4. 验证 Aggregator 反映 Modifier
        Assert.Equal(25f, mgr.GetCurrentValue(target, 0), 0.001f);

        // 5. SG accessor: 读组件 BaseValue (独立于 Aggregator)
        ref var healthRef = ref TestAttrSet.GetHealth(target);
        Assert.Equal(100f, healthRef.BaseValue, 0.001f);
    }
}

/// <summary>集成测试用 AttributeSet：单字段。</summary>
public partial struct TestAttrSet : IAttributeSetComponent
{
    [GameplayAttribute]
    public GameplayAttributeData Health;
}

/// <summary>集成测试用 AttributeSet：多字段。</summary>
public partial struct MultiFieldAttrSet : IAttributeSetComponent
{
    [GameplayAttribute]
    public GameplayAttributeData Strength;

    [GameplayAttribute]
    public GameplayAttributeData Agility;
}
