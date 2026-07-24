// src/Gameplay/GameplayAbilities/GameplayEffect/GameplayEffectSpec.cs
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Gameplay.Tags;

namespace Gameplay.Abilities;

/// <summary>已计算 Magnitude 的单个 Modifier 条目。</summary>
public struct FModifierSpec
{
    public int AttributeId;
    public EGameplayModOp ModOp;
    public float EvaluatedMagnitude;
    public EAttributeCapturePolicy CapturePolicy;
}

/// <summary>
/// GameplayEffect 的施放实例（非 Entity）。创建后不可变（除 StackCount/Duration 运行时调整）。
/// </summary>
public class GameplayEffectSpec
{
    public GameplayEffect Definition { get; }
    public float Level { get; set; }
    public float Duration { get; set; }
    public float Period { get; set; }
    public int StackCount { get; set; } = 1;
    public List<FModifierSpec> Modifiers { get; } = new();
    public GameplayTagContainer CapturedSourceTags { get; } = new();
    public GameplayTagContainer CapturedTargetTags { get; } = new();
    public GameplayTagContainer DynamicAssetTags { get; } = new();
    public GameplayEffectContext? EffectContext { get; set; }

    private Dictionary<GameplayTag, float> setByCallerMagnitudes = new();

    public GameplayEffectSpec(GameplayEffect definition, float level)
    {
        Definition = definition;
        Level = level;
        Duration = -1f; // Instant 场景
        Period = definition.Period;
    }

    public void SetSetByCallerMagnitude(GameplayTag tag, float magnitude)
        => setByCallerMagnitudes[tag] = magnitude;

    public float GetSetByCallerMagnitude(GameplayTag tag, bool warnIfNotFound = true)
        => setByCallerMagnitudes.TryGetValue(tag, out var v) ? v : 0f;
}

/// <summary>Effect 施放上下文（Instigator 信息等）。</summary>
public class GameplayEffectContext
{
    public Entity? Instigator;
    public int InstigatorAbilityHandle;
}
