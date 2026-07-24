// src/Gameplay/GameplayAbilities/GameplayEffect/EffectSystem.cs
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Gameplay.Tags;

namespace Gameplay.Abilities;

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

    // 延迟 Apply 队列（OnCompleteEffects / OnApplicationEffects 产生的新 GE）
    private readonly List<(GameplayEffectSpec spec, Entity target)> deferredApplies = new();

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

        // 延迟 Apply（OnCompleteEffects 链接触发，避免 Query 内结构修改）
        for (int i = 0; i < deferredApplies.Count; i++)
        {
            var (chainSpec, chainTarget) = deferredApplies[i];
            Apply(chainSpec, chainTarget);
        }
        deferredApplies.Clear();
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

        // Immunity: 遍历 Target 已有 ActiveGE => 检查 ImmunityQueries
        foreach (var child in target.ChildEntities)
        {
            if (child.TryGetComponent<ActiveGameplayEffectComponent>(out var activeGE))
            {
                if (handleToSpec.TryGetValue(activeGE.Handle, out var activeSpec))
                {
                    var activeDef = activeSpec.Definition;
                    // activeDef 的 ImmunityQueries 匹配 incoming spec → 免疫
                    foreach (var query in activeDef.ImmunityQueries)
                    {
                        if (query.Matches(spec))
                            return false;
                    }
                }
            }
        }

        return true;
    }

    public int Apply(GameplayEffectSpec spec, Entity target)
    {
        // 1. PreApply: RemoveOtherEffects — 先收集 handles，迭代完再移除，防止 ChildEntities 被修改
        var ge = spec.Definition;
        var toRemove = new List<int>();
        foreach (var removeQuery in ge.RemoveOtherEffectsQueries)
        {
            foreach (var child in target.ChildEntities)
            {
                if (child.TryGetComponent<ActiveGameplayEffectComponent>(out var activeGE))
                {
                    if (handleToSpec.TryGetValue(activeGE.Handle, out var existingSpec))
                    {
                        if (removeQuery.Matches(existingSpec))
                            toRemove.Add(activeGE.Handle);
                    }
                }
            }
        }
        foreach (var h in toRemove)
            RemoveEffect(h, EEffectEndType.Premature);

        // 2. Stacking: 检查 target 上是否有同源 GE（同一个 Definition）
        foreach (var child in target.ChildEntities)
        {
            if (child.TryGetComponent<ActiveGameplayEffectComponent>(out var existing))
            {
                if (handleToSpec.TryGetValue(existing.Handle, out var existingSpec) &&
                    existingSpec.Definition == ge)
                {
                    // 同源 GE → Stack
                    int newCount = existing.StackCount + spec.StackCount;
                    if (newCount > existing.StackLimit) return -1;
                    existing.StackCount = newCount;
                    switch (existing.StackingDurationPolicy)
                    {
                        case EGameplayEffectStackingDurationPolicy.RefreshOnSuccessfulApplication:
                            existing.Duration = spec.Duration; break;
                        case EGameplayEffectStackingDurationPolicy.ExtendDuration:
                            existing.Duration += spec.Duration; break;
                    }
                    if (existing.StackingPeriodPolicy == EGameplayEffectStackingPeriodPolicy.ResetOnSuccessfulApplication)
                        existing.PeriodProgress = 0f;
                    child.GetComponent<ActiveGameplayEffectComponent>() = existing;
                    return existing.Handle;
                }
            }
        }

        // 3. CanApply
        if (!CanApply(spec, target)) return -1;

        int handle = nextHandle++;

        // 3. Create ActiveGameplayEffect Entity as child of target
        var entity = target.Store.CreateEntity();
        target.AddChild(entity);

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

        // 5. Apply Modifiers -> AttributeSystem（仅 Persistent + ExecuteOnApply）
        foreach (var mod in spec.Modifiers)
        {
            if (target.HasComponent<DirtyAttributeComponent>())
            {
                if (mod.ExecutionType == EModifierExecutionType.Persistent)
                {
                    if (!attributeSystem.HasAggregator(target, mod.AttributeId))
                        attributeSystem.SetAggregatorValue(target, mod.AttributeId, 0f);
                    attributeSystem.AddAggregatorMod(target, mod.AttributeId, handle,
                        mod.EvaluatedMagnitude, mod.ModOp);
                    ref var dirty = ref target.GetComponent<DirtyAttributeComponent>();
                    dirty.SetBit(mod.AttributeId);
                }
                else if (mod.ExecutionType == EModifierExecutionType.ExecuteOnApply)
                {
                    float baseVal = attributeSystem.GetBaseValue(target, mod.AttributeId);
                    float newBase = ApplyModOp(baseVal, mod.ModOp, mod.EvaluatedMagnitude);
                    attributeSystem.SetAggregatorValue(target, mod.AttributeId, newBase);
                    ref var dirty = ref target.GetComponent<DirtyAttributeComponent>();
                    dirty.SetBit(mod.AttributeId);
                }
            }
        }

        // 6. Add GrantedTags to target
        if (comp.GrantedTags.Count > 0 && target.HasComponent<GameplayTagsComponent>())
        {
            ref var tags = ref target.GetComponent<GameplayTagsComponent>();
            foreach (var tag in comp.GrantedTags)
                tags.AddTag(tag);
        }

        // 7. OnApplicationEffects: 链接触发其他 GE
        if (ge.OnApplicationEffects.Length > 0)
        {
            foreach (var condEffect in ge.OnApplicationEffects)
            {
                if (condEffect.Effect != null && (condEffect.RequiredSourceTags?.Count ?? 0) == 0)
                {
                    var chainSpec = new GameplayEffectSpec(condEffect.Effect, spec.Level);
                    Apply(chainSpec, target);
                }
            }
        }

        return handle;
    }

    // 重入保护：OnCompleteEffects 触发的 Apply → RemoveOtherEffects 不应再回到当前 handle
    private readonly HashSet<int> removingHandles = new();

    public void RemoveEffect(int handle, EEffectEndType reason)
    {
        if (!removingHandles.Add(handle)) return; // 防止递归

        // Always remove mods from aggregator (works regardless of cache)
        attributeSystem.RemoveAggregatorModsByHandle(handle);

        // Remove GrantedTags + trigger OnCompleteEffects (only if we have cached spec + entity)
        if (handleToSpec.TryGetValue(handle, out var spec))
        {
            if (spec.Definition.GrantedTags.Count > 0)
            {
                if (handleToEntity.TryGetValue(handle, out var entity))
                {
                    var comp = entity.GetComponent<ActiveGameplayEffectComponent>();
                    var target = comp.TargetEntity;
                    if (target.HasComponent<GameplayTagsComponent>())
                    {
                        ref var tags = ref target.GetComponent<GameplayTagsComponent>();
                        foreach (var tag in spec.Definition.GrantedTags)
                            tags.RemoveTag(tag);
                    }
                }
            }

            // OnCompleteEffects: 链接触发其他 GE
            if (spec.Definition.OnCompleteEffects.Length > 0)
            {
                foreach (var condEffect in spec.Definition.OnCompleteEffects)
                {
                    if (condEffect.Effect != null && (condEffect.RequiredSourceTags?.Count ?? 0) == 0)
                    {
                        var chainSpec = new GameplayEffectSpec(condEffect.Effect, spec.Level);
                        if (handleToEntity.TryGetValue(handle, out var entity))
                        {
                            var comp = entity.GetComponent<ActiveGameplayEffectComponent>();
                            deferredApplies.Add((chainSpec, comp.TargetEntity));
                        }
                    }
                }
            }

            // Remove from Handle caches
            handleToSpec.Remove(handle);
            handleToEntity.Remove(handle);
        }

        removingHandles.Remove(handle);
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

    /// <summary>
    /// 周期性执行 Modifier：ExecuteOnPeriod 类型不注册 Aggregator，直接修改 BaseValue。
    /// Persistent 类型已在 Apply 时注册，此处不重复（避免累加）。
    /// </summary>
    private void ExecutePeriodicModifiers(ref ActiveGameplayEffectComponent comp)
    {
        var spec = GetSpecFromHandle(comp.Handle);
        if (spec == null) return;

        var target = comp.TargetEntity;
        if (!target.HasComponent<DirtyAttributeComponent>()) return;
        ref var dirty = ref target.GetComponent<DirtyAttributeComponent>();

        foreach (var mod in spec.Modifiers)
        {
            if (mod.ExecutionType != EModifierExecutionType.ExecuteOnPeriod)
                continue;

            // 直接修改 Aggregator 的 BaseValue，不注册新 Mod
            float baseVal = attributeSystem.GetBaseValue(target, mod.AttributeId);
            float newBase = ApplyModOp(baseVal, mod.ModOp, mod.EvaluatedMagnitude);
            attributeSystem.SetAggregatorValue(target, mod.AttributeId, newBase);
            dirty.SetBit(mod.AttributeId);
        }
    }

    /// <summary>将单个 ModOp 应用到 BaseValue 上，用于 ExecuteOnApply / ExecuteOnPeriod。</summary>
    private static float ApplyModOp(float baseValue, EGameplayModOp op, float magnitude)
        => op switch
        {
            EGameplayModOp.Additive => baseValue + magnitude,
            EGameplayModOp.Multiply => baseValue * magnitude,
            EGameplayModOp.Divide => magnitude != 0f ? baseValue / magnitude : baseValue,
            EGameplayModOp.Override => magnitude,
            EGameplayModOp.FinalAdd => baseValue + magnitude, // FinalAdd 等价 Additive（作用于 Base）
            _ => baseValue + magnitude,
        };

    /// <summary>按 Handle 查询 ActiveGE Entity。</summary>
    public Entity GetEntityByHandle(int handle)
        => handleToEntity.TryGetValue(handle, out var entity) ? entity : default;

    // ── Handle -> Spec 取回 ──
    private GameplayEffectSpec? GetSpecFromHandle(int handle)
        => handleToSpec.TryGetValue(handle, out var spec) ? spec : null;
}
