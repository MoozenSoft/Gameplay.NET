// src/Gameplay/GameplayAbilities/Attribute/GameplayAttributeData.cs
namespace Gameplay.Abilities;

/// <summary>属性值容器。纯数据，Aggregator 负责计算 CurrentValue。</summary>
public struct GameplayAttributeData
{
    /// <summary>永久基础值（升级加点等）。</summary>
    public float BaseValue;

    /// <summary>计算后的当前值 = Evaluate(BaseValue, Mods)。由 AttributeSystem 写入。</summary>
    public float CurrentValue;
}
