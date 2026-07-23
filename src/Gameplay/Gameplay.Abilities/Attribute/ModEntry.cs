// src/Gameplay/GameplayAbilities/Attribute/ModEntry.cs
namespace Gameplay.Abilities;

/// <summary>AttributeAggregator 中的单个 Mod 条目（internal，框架内部使用）。</summary>
internal struct ModEntry
{
    public int ActiveHandle;     // 归属的 ActiveGameplayEffect.Handle
    public float Magnitude;      // 已计算的幅度
}
