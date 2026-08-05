using Friflo.Engine.ECS;
using Gameplay.Abilities;

namespace Gameplay.Tasks;

/// <summary>属性监听条件。</summary>
public enum EAttributeCondition
{
    /// <summary>值发生变化（相对注册时快照，Count 次）。</summary>
    Changed,
    /// <summary>CurrentValue 大于 Threshold。</summary>
    Above,
    /// <summary>CurrentValue 小于 Threshold。</summary>
    Below,
    /// <summary>CurrentValue / BaseValue 大于 Threshold。</summary>
    RatioAbove,
    /// <summary>CurrentValue / BaseValue 小于 Threshold。</summary>
    RatioBelow,
}

/// <summary>属性监听能力——等待目标 Entity 的指定 GameplayAttribute 满足条件（变化/阈值/比值阈值）。</summary>
/// <remarks>
/// <para>读取的是上一帧 Phase 4 Flush 后的已结算值，具有确定性。</para>
/// <para><see cref="Changed"/> 用 <see cref="LastValue"/> + <see cref="Count"/>；阈值模式用 <see cref="Threshold"/>。</para>
/// </remarks>
public struct AttributeListener : IComponent
{
    /// <summary>监听谁身上的属性（玩家 / 任意 Entity）。</summary>
    public Entity Target;

    /// <summary>监听的属性。</summary>
    public GameplayAttribute Attribute;

    /// <summary>监听条件（Changed / Above / Below / RatioAbove / RatioBelow）。</summary>
    public EAttributeCondition Condition;

    /// <summary>Changed 模式：注册时的快照值，用于比较变化。</summary>
    public float LastValue;

    /// <summary>阈值模式：比较阈值（Above/Below 比较 CurrentValue；Ratio 比较 CurrentValue/BaseValue）。</summary>
    public float Threshold;

    /// <summary>Changed 模式：等待次数（大于 0 表示等待多少次变化）。</summary>
    public int Count;
}
