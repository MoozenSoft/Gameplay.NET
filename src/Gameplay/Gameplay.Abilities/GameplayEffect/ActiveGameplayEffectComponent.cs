// src/Gameplay/GameplayAbilities/GameplayEffect/ActiveGameplayEffectComponent.cs
using Friflo.Engine.ECS;
using Gameplay.Tags;

namespace Gameplay.Abilities;

/// <summary>
/// ActiveGameplayEffect 的运行时 Component（单一，所有字段合一）。
/// 挂在 Target Entity 下的子 Entity 上。
/// </summary>
public struct ActiveGameplayEffectComponent : IComponent
{
    // ── 时间 ──
    public float Duration;                       // 剩余时间（Infinite = -1），EffectSystem 每帧递减
    public float StartWorldTime;                 // 开始时间戳——GetTimeRemaining() + Server→Client 同步

    // ── 周期 ──
    public float Period;                         // 周期间隔
    public float PeriodProgress;                 // 当前周期进度

    // ── 堆叠 ──
    public int StackCount;                       // 当前层数
    public int StackLimit;                       // 最大层数
    public EGameplayEffectStackingDurationPolicy StackingDurationPolicy;
    public EGameplayEffectStackingPeriodPolicy StackingPeriodPolicy;
    public EGameplayEffectStackingExpirationPolicy StackingExpirationPolicy;

    // ── 句柄与引用 ──
    public int Handle;                           // 全局唯一 ID
    public Entity SourceEntity;                  // 施放者
    public Entity TargetEntity;                  // 目标（父 Entity）
    public int DefinitionId;                     // GameplayEffectRegistry 查表 key（避免托管引用）

    // ── 抑制 ──
    public bool IsInhibited;                     // Tag 条件不满足时 = true
    public EGameplayEffectPeriodInhibitionRemovedPolicy InhibitionPolicy;

    // ── 行为配置（从 GameplayEffect 拷贝，NULL/empty = 不适用） ──
    public GameplayTagContainer ApplicationRequiredTags;
    public GameplayTagContainer OngoingRequiredTags;
    public GameplayTagContainer RemovalTags;
    public GameplayTagContainer GrantedTags;
    public GameplayTagContainer BlockedAbilityTags;
    public GameplayTagContainer CancelAbilityTags;
}
