namespace Gameplay.Tests.Abilities;

using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Gameplay.Abilities;
using Gameplay.Tags;
using Xunit;

public class EffectSystemIntegrationTests
{
    static EffectSystemIntegrationTests()
    {
        GameplayTagManager.RegisterTags(
            "Buff.Fire", "State.Frozen", "Buff.Shield", "Buff.After");
    }
    // ── Stacking ──

    [Fact]
    public void Stacking_SameEffectTwice_IncreasesStackCount()
    {
        var store = new EntityStore();
        var attrSys = new AttributeSystem();
        var effectSys = new EffectSystem(attrSys);

        var ge = new GameplayEffect
        {
            DurationPolicy = EGameplayEffectDurationType.HasDuration,
            StackLimit = 3,
            StackingDurationPolicy = EGameplayEffectStackingDurationPolicy.NeverRefresh,
        };
        var target = store.CreateEntity();
        target.AddComponent(new DirtyAttributeComponent());

        int handle = effectSys.Apply(new GameplayEffectSpec(ge, 1f) { Duration = 10f, StackCount = 1 }, target);
        Assert.True(handle > 0);

        // 第二次 Apply → 同源 GE 应该 Stack 而非创建新 Entity
        int handle2 = effectSys.Apply(new GameplayEffectSpec(ge, 1f) { Duration = 8f, StackCount = 1 }, target);
        Assert.Equal(handle, handle2);

        var activeEntity = effectSys.GetEntityByHandle(handle);
        var comp = activeEntity.GetComponent<ActiveGameplayEffectComponent>();
        Assert.Equal(2, comp.StackCount);
    }

    [Fact]
    public void Stacking_ExceedsLimit_Rejects()
    {
        var store = new EntityStore();
        var attrSys = new AttributeSystem();
        var effectSys = new EffectSystem(attrSys);

        var ge = new GameplayEffect
        {
            DurationPolicy = EGameplayEffectDurationType.HasDuration,
            StackLimit = 1,
        };
        var target = store.CreateEntity();
        target.AddComponent(new DirtyAttributeComponent());

        int h1 = effectSys.Apply(new GameplayEffectSpec(ge, 1f) { Duration = 10f }, target);
        Assert.True(h1 > 0);

        // StackLimit=1 + StackCount()=1 → 不允许新 Stack
        int h2 = effectSys.Apply(new GameplayEffectSpec(ge, 1f) { Duration = 10f }, target);
        Assert.Equal(-1, h2);
    }

    // ── Immunity ──

    [Fact]
    public void Immunity_BlocksApplication()
    {
        var store = new EntityStore();
        var attrSys = new AttributeSystem();
        var effectSys = new EffectSystem(attrSys);

        // Step 1: Apply a GE that grants immunity to "Buff.Fire"
        var immunityGE = new GameplayEffect
        {
            DurationPolicy = EGameplayEffectDurationType.HasDuration,
            ImmunityQueries = new[]
            {
                GameplayEffectQuery.MakeQuery_MatchAnyGrantedTags(
                    new() { GameplayTag.Request("Buff.Fire") })
            },
        };
        var target = store.CreateEntity();
        target.AddComponent(new DirtyAttributeComponent());
        int h = effectSys.Apply(new GameplayEffectSpec(immunityGE, 1f) { Duration = 10f }, target);
        Assert.True(h > 0);

        // Step 2: Try to apply "Buff.Fire" GE
        var fireGE = new GameplayEffect
        {
            DurationPolicy = EGameplayEffectDurationType.HasDuration,
            GrantedTags = new() { GameplayTag.Request("Buff.Fire") },
        };

        int rejected = effectSys.Apply(new GameplayEffectSpec(fireGE, 1f) { Duration = 5f }, target);
        Assert.Equal(-1, rejected);
    }

    [Fact]
    public void CanApply_RespectsChanceToApply()
    {
        var store = new EntityStore();
        var attrSys = new AttributeSystem();
        var effectSys = new EffectSystem(attrSys);

        var ge = new GameplayEffect
        {
            DurationPolicy = EGameplayEffectDurationType.HasDuration,
            ChanceToApply = 0f, // Never
        };
        var target = store.CreateEntity();

        Assert.False(effectSys.CanApply(new GameplayEffectSpec(ge, 1f), target));
    }

    // ── RemoveOtherEffects ──

    [Fact]
    public void RemoveOtherEffects_RemovesConflictingEffect()
    {
        var store = new EntityStore();
        var attrSys = new AttributeSystem();
        var effectSys = new EffectSystem(attrSys);

        // Step 1: Apply "Freeze" GE
        var freezeGE = new GameplayEffect
        {
            DurationPolicy = EGameplayEffectDurationType.Infinite,
            GrantedTags = new() { GameplayTag.Request("State.Frozen") },
        };
        var target = store.CreateEntity();
        target.AddComponent(new DirtyAttributeComponent());
        var freezeSpec = new GameplayEffectSpec(freezeGE, 1f) { Duration = -1f };
        int freezeHandle = effectSys.Apply(freezeSpec, target);
        Assert.True(freezeHandle > 0);

        // Step 2: Apply "Burn" GE that removes "Freeze"
        var burnGE = new GameplayEffect
        {
            DurationPolicy = EGameplayEffectDurationType.HasDuration,
            RemoveOtherEffectsQueries = new[]
            {
                GameplayEffectQuery.MakeQuery_MatchAnyGrantedTags(
                    new() { GameplayTag.Request("State.Frozen") })
            },
        };
        var burnSpec = new GameplayEffectSpec(burnGE, 1f) { Duration = 5f };
        int burnHandle = effectSys.Apply(burnSpec, target);
        Assert.True(burnHandle > 0);

        // Freeze entity should have been removed
        Assert.True(effectSys.GetEntityByHandle(freezeHandle).IsNull);
    }

    // ── OnApplicationEffects ──

    [Fact]
    public void OnApplicationEffects_ChainsAdditionalGE()
    {
        var store = new EntityStore();
        var attrSys = new AttributeSystem();
        var effectSys = new EffectSystem(attrSys);

        // GE_B: the chained effect (grants "Buff.Shield" tag)
        var shieldGE = new GameplayEffect
        {
            DurationPolicy = EGameplayEffectDurationType.HasDuration,
            GrantedTags = new() { GameplayTag.Request("Buff.Shield") },
        };

        // GE_A: the main effect with OnApplicationEffects → GE_B
        var mainGE = new GameplayEffect
        {
            DurationPolicy = EGameplayEffectDurationType.HasDuration,
            OnApplicationEffects = new[]
            {
                new ConditionalGameplayEffect { Effect = shieldGE }
            },
        };

        var target = store.CreateEntity();
        target.AddComponent(new DirtyAttributeComponent());
        target.AddComponent(new Gameplay.Tags.GameplayTagsComponent());

        int handle = effectSys.Apply(new GameplayEffectSpec(mainGE, 1f) { Duration = 10f }, target);
        Assert.True(handle > 0);

        // Target now has "Buff.Shield" tag from the chained effect
        var tags = target.GetComponent<Gameplay.Tags.GameplayTagsComponent>();
        Assert.True(tags.HasTag(GameplayTag.Request("Buff.Shield")));
    }

    // ── OnCompleteEffects ──

    [Fact]
    public void OnCompleteEffects_TriggersOnRemove()
    {
        var store = new EntityStore();
        var attrSys = new AttributeSystem();
        var effectSys = new EffectSystem(attrSys);

        var chainGE = new GameplayEffect
        {
            DurationPolicy = EGameplayEffectDurationType.HasDuration,
            GrantedTags = new() { GameplayTag.Request("Buff.After") },
        };

        var mainGE = new GameplayEffect
        {
            DurationPolicy = EGameplayEffectDurationType.HasDuration,
            OnCompleteEffects = new[]
            {
                new ConditionalGameplayEffect { Effect = chainGE }
            },
        };

        var target = store.CreateEntity();
        target.AddComponent(new DirtyAttributeComponent());
        target.AddComponent(new Gameplay.Tags.GameplayTagsComponent());

        int handle = effectSys.Apply(new GameplayEffectSpec(mainGE, 1f) { Duration = 0.5f }, target);
        Assert.True(handle > 0);

        // Run EffectSystem to expire the main GE
        var root = new SystemRoot(store) { effectSys, attrSys };
        root.Update(new UpdateTick(1f, 0));

        // Target now has "Buff.After" tag from OnCompleteEffects
        var tags = target.GetComponent<Gameplay.Tags.GameplayTagsComponent>();
        Assert.True(tags.HasTag(GameplayTag.Request("Buff.After")));
    }
}
