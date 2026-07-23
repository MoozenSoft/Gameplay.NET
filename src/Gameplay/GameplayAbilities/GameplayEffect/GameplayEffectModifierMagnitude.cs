// src/Gameplay/GameplayAbilities/GameplayEffect/GameplayEffectModifierMagnitude.cs
namespace Gameplay.GameplayAbilities;

/// <summary>幅度计算方式。</summary>
public enum EGameplayEffectMagnitudeCalculation
{
    ScalableFloat,
    AttributeBased,
    CustomCalculationClass,
    SetByCaller,
}

/// <summary>GameplayEffect Modifier 的幅度值（4 种计算方式之一）。</summary>
public class GameplayEffectModifierMagnitude
{
    public EGameplayEffectMagnitudeCalculation CalculationType { get; private set; }

    // ScalableFloat
    public float Coefficient { get; private set; }
    public float ScalableValue { get; private set; }

    // AttributeBased
    public float AttrCoefficient { get; private set; }
    public float PreMultiplyAdditive { get; private set; }
    public float PostMultiplyAdditive { get; private set; }
    // 引用的 GameplayAttribute 在 GameplayModifier 中指定

    private GameplayEffectModifierMagnitude() { }

    public static GameplayEffectModifierMagnitude CreateScalableFloat(float coefficient, float value)
        => new() { CalculationType = EGameplayEffectMagnitudeCalculation.ScalableFloat,
                   Coefficient = coefficient, ScalableValue = value };

    public static GameplayEffectModifierMagnitude CreateAttributeBased(
        float coefficient, float preAdd, float postAdd)
        => new() { CalculationType = EGameplayEffectMagnitudeCalculation.AttributeBased,
                   AttrCoefficient = coefficient, PreMultiplyAdditive = preAdd,
                   PostMultiplyAdditive = postAdd };

    public static GameplayEffectModifierMagnitude CreateCustomCalculation()
        => new() { CalculationType = EGameplayEffectMagnitudeCalculation.CustomCalculationClass };

    public static GameplayEffectModifierMagnitude CreateSetByCaller()
        => new() { CalculationType = EGameplayEffectMagnitudeCalculation.SetByCaller };
}
