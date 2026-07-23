using Gameplay.Tags;

namespace Gameplay.Abilities;

/// <summary>
/// GameplayAbility 静态定义（非 Entity）。策划/开发者配置的资产级数据。
/// </summary>
public class GameplayAbility
{
    // ── Tags ──
    public GameplayTagContainer AssetTags = new();
    public GameplayTagContainer CancelAbilitiesWithTag = new();
    public GameplayTagContainer BlockAbilitiesWithTag = new();
    public GameplayTagContainer ActivationOwnedTags = new();
    public GameplayTagContainer ActivationRequiredTags = new();
    public GameplayTagContainer ActivationBlockedTags = new();
    public GameplayTagContainer SourceRequiredTags = new();
    public GameplayTagContainer SourceBlockedTags = new();
    public GameplayTagContainer TargetRequiredTags = new();
    public GameplayTagContainer TargetBlockedTags = new();

    // ── Cooldown ──
    public GameplayEffect? CooldownEffect;

    // ── Triggers ──
    public AbilityTriggerData[] AbilityTriggers = System.Array.Empty<AbilityTriggerData>();

    // ── Network ──
    public EGameplayAbilityNetExecutionPolicy NetExecutionPolicy = EGameplayAbilityNetExecutionPolicy.LocalPredicted;
    public EGameplayAbilityNetSecurityPolicy NetSecurityPolicy;

    // ── Extensions ──
    public IAbilityRequirement[] Requirements = System.Array.Empty<IAbilityRequirement>();
    public IAbilityCommit[] CommitActions = System.Array.Empty<IAbilityCommit>();
    public IAbilityExecutor? Executor;
}

/// <summary>Ability 触发器配置。</summary>
public struct AbilityTriggerData
{
    public GameplayTag TriggerTag;
    public EAbilityTriggerSource TriggerSource;
}

/// <summary>Ability 激活前提条件接口。</summary>
public interface IAbilityRequirement
{
}

/// <summary>Ability 提交动作接口。</summary>
public interface IAbilityCommit
{
}

/// <summary>Ability 执行器接口。</summary>
public interface IAbilityExecutor
{
}
