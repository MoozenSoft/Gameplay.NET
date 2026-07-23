// src/Gameplay/GameplayAbilities/GameplayEffect/EffectSystem.cs
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Gameplay.GameplayTags;

namespace Gameplay.GameplayAbilities;

/// <summary>
/// GameplayEffect 核心 System：Tick Duration / Period / TagRequirements / Expiration。
/// Apply 和 Remove 作为 public API 供外部调用。
/// </summary>
public class EffectSystem : QuerySystem<ActiveGameplayEffectComponent>
{
    private readonly AttributeSystem attributeSystem;
    private int nextHandle = 1;

    // Handle -> Spec 缓存（Apply 时存储，Remove 时取回）
    private readonly Dictionary<int, GameplayEffectSpec> handleToSpec = new();
    // Handle -> Entity 懒查询（Remove 时需要 Entity 来移除 Tags）
    private readonly Dictionary<int, Entity> handleToEntity = new();

    public EffectSystem(AttributeSystem attributeSystem)
    {
        this.attributeSystem = attributeSystem;
    }

    protected override void OnUpdate()
    {
        float dt = Tick.deltaTime;
        Query.ForEachEntity((ref ActiveGameplayEffectComponent comp, Entity entity) =>
        {
            // 1. TickDuration
            if (comp.Duration > 0)
            {
                comp.Duration -= dt;
                if (comp.Duration <= 0)
                {
                    HandleExpiration(ref comp, entity);
                    return;
                }
            }

            // 2. TickPeriod
            if (comp.Period > 0 && !comp.IsInhibited)
            {
                comp.PeriodProgress += dt;
                while (comp.PeriodProgress >= comp.Period)
                {
                    comp.PeriodProgress -= comp.Period;
                    ExecutePeriodicModifiers(ref comp);
                }
            }
        });
    }

    // ── Public API ──

    public bool CanApply(GameplayEffectSpec spec, Entity target)
    {
        var ge = spec.Definition;

        // ApplicationRequiredTags check
        if (ge.ApplicationRequiredTags.Count > 0)
        {
            if (!target.TryGetComponent<GameplayTagsComponent>(out var tags))
                return false;
            if (!tags.HasAll(ge.ApplicationRequiredTags))
                return false;
        }

        // ChanceToApply
        if (ge.ChanceToApply < 1.0f)
        {
            if ((float)new System.Random().NextDouble() > ge.ChanceToApply)
                return false;
        }

        // TODO: Immunity check (needs target ActiveEffects access)

        return true;
    }

    public int Apply(GameplayEffectSpec spec, Entity target)
    {
        // 1. PreApply: RemoveOtherEffects (TODO)
        // 2. CanApply
        if (!CanApply(spec, target)) return -1;

        int handle = nextHandle++;

        // 3. Create ActiveGameplayEffect Entity as child of target
        var entity = target.Store.CreateEntity();
        entity.AddChild(target);

        var comp = new ActiveGameplayEffectComponent
        {
            Duration = spec.Duration,
            Period = spec.Period,
            StartWorldTime = 0f, // TODO: use world time
            Handle = handle,
            SourceEntity = spec.EffectContext?.Instigator ?? default,
            TargetEntity = target,
            DefinitionId = 0,
            StackCount = spec.StackCount,
            StackLimit = spec.Definition.StackLimit,
            StackingDurationPolicy = spec.Definition.StackingDurationPolicy,
            StackingPeriodPolicy = spec.Definition.StackingPeriodPolicy,
            StackingExpirationPolicy = spec.Definition.StackingExpirationPolicy,
            InhibitionPolicy = spec.Definition.InhibitionPolicy,
            ApplicationRequiredTags = spec.Definition.ApplicationRequiredTags,
            OngoingRequiredTags = spec.Definition.OngoingRequiredTags,
            RemovalTags = spec.Definition.RemovalTags,
            GrantedTags = spec.Definition.GrantedTags,
            BlockedAbilityTags = spec.Definition.BlockedAbilityTags,
            CancelAbilityTags = spec.Definition.CancelAbilityTags,
        };

        entity.AddComponent(comp);

        // 4. Cache Spec and Entity for later RemoveEffect lookup
        handleToSpec[handle] = spec;
        handleToEntity[handle] = entity;

        // 5. Apply Modifiers -> AttributeSystem
        foreach (var mod in spec.Modifiers)
        {
            if (target.TryGetComponent<DirtyAttributeComponent>(out var dirty))
            {
                attributeSystem.SetAggregatorValue(target, mod.AttributeId, baseValue: 0f);
                attributeSystem.AddAggregatorMod(target, mod.AttributeId, handle,
                    mod.EvaluatedMagnitude, mod.ModOp);
                dirty.SetBit(mod.AttributeId);
            }
        }

        // 6. Add GrantedTags to target
        if (comp.GrantedTags.Count > 0)
        {
            if (target.TryGetComponent<GameplayTagsComponent>(out var tags))
            {
                foreach (var tag in comp.GrantedTags)
                    tags.AddTag(tag);
            }
        }

        // 7. OnApplicationEffects (TODO)

        return handle;
    }

    public void RemoveEffect(int handle, EEffectEndType reason)
    {
        // Always remove mods from aggregator (works regardless of cache)
        attributeSystem.RemoveAggregatorModsByHandle(handle);

        // Remove GrantedTags (only if we have cached spec + entity)
        if (handleToSpec.TryGetValue(handle, out var spec))
        {
            if (spec.Definition.GrantedTags.Count > 0)
            {
                if (handleToEntity.TryGetValue(handle, out var entity))
                {
                    var comp = entity.GetComponent<ActiveGameplayEffectComponent>();
                    var target = comp.TargetEntity;
                    if (target.TryGetComponent<GameplayTagsComponent>(out var tags))
                    {
                        foreach (var tag in spec.Definition.GrantedTags)
                            tags.RemoveTag(tag);
                    }
                }
            }

            // Remove from Handle caches
            handleToSpec.Remove(handle);
            handleToEntity.Remove(handle);
        }

        // Entity destruction handled by OnRemoveEntityTags (Task 12)
    }

    private void HandleExpiration(ref ActiveGameplayEffectComponent comp, Entity entity)
    {
        if (comp.StackCount > 1)
        {
            comp.StackCount--;
            switch (comp.StackingExpirationPolicy)
            {
                case EGameplayEffectStackingExpirationPolicy.ClearEntireStack:
                    RemoveEffect(comp.Handle, EEffectEndType.Normal);
                    break;
                case EGameplayEffectStackingExpirationPolicy.RemoveSingleStackAndRefreshDuration:
                    // Duration refreshed (set when this Stack was applied)
                    break;
                case EGameplayEffectStackingExpirationPolicy.RefreshDuration:
                    // Duration stays, manual management
                    break;
            }
        }
        else
        {
            RemoveEffect(comp.Handle, EEffectEndType.Normal);
        }
    }

    private void ExecutePeriodicModifiers(ref ActiveGameplayEffectComponent comp)
    {
        // Task 11 填充：遍历每个 Modifier -> Aggregator -> SetDirty
    }

    // ── Handle -> Spec 取回 ──
    private GameplayEffectSpec? GetSpecFromHandle(int handle)
        => handleToSpec.TryGetValue(handle, out var spec) ? spec : null;
}
