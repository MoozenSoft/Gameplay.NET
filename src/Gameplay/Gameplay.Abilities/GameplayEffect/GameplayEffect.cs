// src/Gameplay/GameplayAbilities/GameplayEffect/GameplayEffect.cs
using System.Collections.Generic;
using Gameplay.Tags;

namespace Gameplay.Abilities;

/// <summary>
/// GameplayEffect 静态定义（非 Entity）。策划/开发者配置的资产级数据。
/// </summary>
public class GameplayEffect
{
    // ── 基础 ──
    public EGameplayEffectDurationType DurationPolicy = EGameplayEffectDurationType.Instant;
    public int StackLimit = 1;
    public EGameplayEffectStackingDurationPolicy StackingDurationPolicy;
    public EGameplayEffectStackingPeriodPolicy StackingPeriodPolicy;
    public EGameplayEffectStackingExpirationPolicy StackingExpirationPolicy;
    public float Period;
    public EGameplayEffectPeriodInhibitionRemovedPolicy InhibitionPolicy;

    // ── Modifiers ──
    public List<GameplayModifier> Modifiers = new();

    // ── Tag 条件 ──
    public GameplayTagContainer ApplicationRequiredTags = new();
    public GameplayTagContainer OngoingRequiredTags = new();
    public GameplayTagContainer RemovalTags = new();

    // ── 副作用 ──
    public GameplayTagContainer GrantedTags = new();
    public GameplayTagContainer BlockedAbilityTags = new();
    public GameplayTagContainer CancelAbilityTags = new();

    // ── 其他 ──
    public float ChanceToApply = 1.0f;
    public GameplayEffectQuery[] ImmunityQueries = System.Array.Empty<GameplayEffectQuery>();
    public GameplayEffectQuery[] RemoveOtherEffectsQueries = System.Array.Empty<GameplayEffectQuery>();
    public ConditionalGameplayEffect[] OnApplicationEffects = System.Array.Empty<ConditionalGameplayEffect>();
    public ConditionalGameplayEffect[] OnCompleteEffects = System.Array.Empty<ConditionalGameplayEffect>();
    public GameplayEffectCue[] CueDefinitions = System.Array.Empty<GameplayEffectCue>();
}
