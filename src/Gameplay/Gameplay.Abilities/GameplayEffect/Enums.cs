// src/Gameplay/GameplayAbilities/GameplayEffect/Enums.cs
namespace Gameplay.Abilities;

/// <summary>GE 的持续时间策略。</summary>
public enum EGameplayEffectDurationType
{
    Instant,       // 立即执行，不创建 ActiveEntity
    HasDuration,   // 有限时长
    Infinite,      // 无限时长
}

/// <summary>Modifier 运算类型。</summary>
public enum EGameplayModOp
{
    Additive,      // Base + SAdd
    Multiply,      // x ΠMultiply
    Divide,        // / ΠDivide
    Override,      // = OverrideValue
    FinalAdd,      // ... + SFinalAdd
}

/// <summary>Modifier 执行时机。</summary>
public enum EModifierExecutionType
{
    Persistent,       // 持续生效（默认）
    ExecuteOnApply,   // 应用时执行一次
    ExecuteOnPeriod,  // 每个周期执行
}

/// <summary>堆叠时 Duration 策略。</summary>
public enum EGameplayEffectStackingDurationPolicy
{
    RefreshOnSuccessfulApplication,
    NeverRefresh,
    ExtendDuration,
}

/// <summary>堆叠时 Period 策略。</summary>
public enum EGameplayEffectStackingPeriodPolicy
{
    ResetOnSuccessfulApplication,
    NeverReset,
}

/// <summary>堆叠到期策略。</summary>
public enum EGameplayEffectStackingExpirationPolicy
{
    ClearEntireStack,
    RemoveSingleStackAndRefreshDuration,
    RefreshDuration,
}

/// <summary>Inhibition 解除后 Period 策略。</summary>
public enum EGameplayEffectPeriodInhibitionRemovedPolicy
{
    NeverReset,
    ResetPeriod,
    ExecuteAndResetPeriod,
}

/// <summary>Modifier 属性抓取策略。</summary>
public enum EAttributeCapturePolicy
{
    Snapshot,   // Spec 创建时抓取一次
    RealTime,   // 每次 Execute 实时抓取
}

/// <summary>Effect 结束原因。</summary>
public enum EEffectEndType
{
    Normal,       // Duration 自然到期 / StackCount 归零
    Premature,    // RemoveEffect() 主动移除 / RemoveOtherEffects / RemovalTags
}
