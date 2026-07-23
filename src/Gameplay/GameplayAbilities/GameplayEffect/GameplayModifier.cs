// src/Gameplay/GameplayAbilities/GameplayEffect/GameplayModifier.cs
namespace Gameplay.GameplayAbilities;

/// <summary>GameplayEffect 的单个 Modifier 定义——改哪个属性 + 怎么算 + 什么操作。</summary>
public struct GameplayModifier
{
    /// <summary>修改的目标属性（SG 生成的 GameplayAttribute 句柄）。</summary>
    public int AttributeId;

    /// <summary>运算类型。</summary>
    public EGameplayModOp ModOp;

    /// <summary>幅度定义。</summary>
    public GameplayEffectModifierMagnitude MagnitudeCalc;

    /// <summary>属性抓取策略。</summary>
    public EAttributeCapturePolicy CapturePolicy;

    /// <summary>Modifier 执行类型：Persistent / ExecuteOnApply / ExecuteOnPeriod。</summary>
    public EModifierExecutionType ExecutionType;

    // TagRequirements 在指定 source/target 时必须满足才生效（后续 EffectSystem 实现）
}
