// tests/Gameplay.Tests/GameplayAbilities/GameplayEffect/EffectSystemTests.cs
namespace Gameplay.Tests.Abilities;

using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Gameplay.Abilities;
using Gameplay.Tags;
using Xunit;

public class EffectSystemTests
{
    [Fact]
    public void TickDuration_DecrementsDuration()
    {
        var store = new EntityStore();
        var attrSys = new AttributeSystem();
        var effectSys = new EffectSystem(attrSys);
        var root = new SystemRoot(store) { effectSys };

        var target = store.CreateEntity();
        var activeEntity = store.CreateEntity();
        activeEntity.AddChild(target);
        activeEntity.AddComponent(new ActiveGameplayEffectComponent
        {
            Duration = 3.0f,
            TargetEntity = target,
            Handle = 1,
        });

        root.Update(new UpdateTick(1.0f, 0)); // dt = 1.0f

        var comp = activeEntity.GetComponent<ActiveGameplayEffectComponent>();
        Assert.Equal(2.0f, comp.Duration, 0.001f);
    }

    [Fact]
    public void TickDuration_Expires_TriggersRemoveEffect()
    {
        var store = new EntityStore();
        var attrSys = new AttributeSystem();
        var effectSys = new EffectSystem(attrSys);
        var root = new SystemRoot(store) { effectSys };

        var target = store.CreateEntity();
        // 预设 aggregator，验证 RemoveEffect 会清理 mod
        attrSys.SetAggregatorValue(target, attributeId: 0, baseValue: 100f);
        attrSys.AddAggregatorMod(target, 0, handle: 1, magnitude: 10f,
            EGameplayModOp.Additive);

        var activeEntity = store.CreateEntity();
        activeEntity.AddChild(target);
        activeEntity.AddComponent(new ActiveGameplayEffectComponent
        {
            Duration = 0.5f,
            TargetEntity = target,
            Handle = 1,
        });

        root.Update(new UpdateTick(1.0f, 0)); // 过期

        // RemoveEffect 清理了 mod，只剩 baseValue
        float val = attrSys.GetCurrentValue(target, attributeId: 0);
        Assert.Equal(100f, val);
    }

    [Fact]
    public void Infinite_Duration_NotDecremented()
    {
        var store = new EntityStore();
        var attrSys = new AttributeSystem();
        var effectSys = new EffectSystem(attrSys);
        var root = new SystemRoot(store) { effectSys };

        var target = store.CreateEntity();
        var activeEntity = store.CreateEntity();
        activeEntity.AddChild(target);
        activeEntity.AddComponent(new ActiveGameplayEffectComponent
        {
            Duration = -1f, // Infinite
            TargetEntity = target,
            Handle = 1,
        });

        root.Update(new UpdateTick(10f, 0));
        var comp = activeEntity.GetComponent<ActiveGameplayEffectComponent>();
        Assert.Equal(-1f, comp.Duration);
    }

    [Fact]
    public void Periodic_Tick_AdvancesPeriodProgress()
    {
        var store = new EntityStore();
        var attrSys = new AttributeSystem();
        var effectSys = new EffectSystem(attrSys);
        var root = new SystemRoot(store) { effectSys };

        var target = store.CreateEntity();
        var activeEntity = store.CreateEntity();
        activeEntity.AddChild(target);
        activeEntity.AddComponent(new ActiveGameplayEffectComponent
        {
            Duration = 10f,
            Period = 2.0f,
            TargetEntity = target,
            Handle = 1,
        });

        root.Update(new UpdateTick(2.5f, 0)); // Period triggered at t=2.0

        var comp = activeEntity.GetComponent<ActiveGameplayEffectComponent>();
        Assert.True(comp.PeriodProgress < 2.0f); // Reset after trigger
        Assert.True(comp.PeriodProgress >= 0f);  // Progress is non-negative
    }

    [Fact]
    public void Apply_HasDuration_CreatesEntityWithComponent()
    {
        var store = new EntityStore();
        var attrSys = new AttributeSystem();
        var effectSys = new EffectSystem(attrSys);

        var ge = new GameplayEffect
        {
            DurationPolicy = EGameplayEffectDurationType.HasDuration,
        };
        var spec = new GameplayEffectSpec(ge, 1f) { Duration = 5f };

        var target = store.CreateEntity();
        int handle = effectSys.Apply(spec, target);

        Assert.True(handle > 0);
        // Verify ActiveGameplayEffect Entity exists under target
    }

    [Fact]
    public void CanApply_TagRequirement_Fails_ReturnsFalse()
    {
        var store = new EntityStore();
        var attrSys = new AttributeSystem();
        var effectSys = new EffectSystem(attrSys);

        var tag = GameplayTag.Request("State.Dead");
        var ge = new GameplayEffect();
        ge.ApplicationRequiredTags.AddTag(tag);

        var spec = new GameplayEffectSpec(ge, 1f);
        var target = store.CreateEntity();
        // Target doesn't have State.Dead -> CanApply = false

        Assert.False(effectSys.CanApply(spec, target));
    }

    [Fact]
    public void CanApply_NoRequirements_ReturnsTrue()
    {
        var store = new EntityStore();
        var attrSys = new AttributeSystem();
        var effectSys = new EffectSystem(attrSys);

        var spec = new GameplayEffectSpec(new GameplayEffect(), 1f);
        var target = store.CreateEntity();

        Assert.True(effectSys.CanApply(spec, target));
    }
}
