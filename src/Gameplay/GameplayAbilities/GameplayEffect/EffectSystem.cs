// src/Gameplay/GameplayAbilities/GameplayEffect/EffectSystem.cs
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;

namespace Gameplay.GameplayAbilities;

/// <summary>
/// GameplayEffect 核心 System：Tick Duration / Period / TagRequirements / Expiration。
/// Apply 和 Remove 作为 public API 供外部调用。
/// </summary>
public class EffectSystem : QuerySystem<ActiveGameplayEffectComponent>
{
    private readonly AttributeSystem attributeSystem;
    private int nextHandle = 1;

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

    private void HandleExpiration(ref ActiveGameplayEffectComponent comp, Entity entity)
    {
        if (comp.StackCount > 1)
        {
            comp.StackCount--;
            switch (comp.StackingExpirationPolicy)
            {
                case EGameplayEffectStackingExpirationPolicy.ClearEntireStack:
                    RemoveEffect(comp.Handle, EffectEndType.Normal);
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
            RemoveEffect(comp.Handle, EffectEndType.Normal);
        }
    }

    private void ExecutePeriodicModifiers(ref ActiveGameplayEffectComponent comp)
    {
        // Task 11 填充：遍历每个 Modifier → Aggregator → SetDirty
    }

    // ── 待 Task 11 (Apply) 补充的占位 ──
    private GameplayEffectSpec? GetSpecFromHandle(int handle) => null; // Task 11 替换

    // ── Placeholder Remove ──
    public void RemoveEffect(int handle, EffectEndType reason)
    {
        attributeSystem.RemoveAggregatorModsByHandle(handle);
        // Entity 销毁 → Task 11 补充完整流程
    }
}
