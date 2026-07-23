// src/Gameplay/GameplayAbilities/Attribute/AttributeAggregator.cs
using System.Collections.Generic;

namespace Gameplay.GameplayAbilities;

/// <summary>
/// 单个 GameplayAttribute 的运行时聚合器。
/// 不是 Component，由 AttributeSystem 内部 Dictionary 管理。
/// </summary>
internal class AttributeAggregator
{
    public float BaseValue;
    public bool Dirty;

    // ModBuckets[(int)EGameplayModOp] — 按 ModOp 分桶
    private List<ModEntry>[] modBuckets;

    public AttributeAggregator()
    {
        int opCount = System.Enum.GetValues<EGameplayModOp>().Length;
        modBuckets = new List<ModEntry>[opCount];
        for (int i = 0; i < opCount; i++)
            modBuckets[i] = new List<ModEntry>();
    }

    public void AddMod(int handle, float magnitude, EGameplayModOp op)
    {
        modBuckets[(int)op].Add(new ModEntry { ActiveHandle = handle, Magnitude = magnitude });
        Dirty = true;
    }

    public void RemoveModsByHandle(int handle)
    {
        for (int i = 0; i < modBuckets.Length; i++)
            modBuckets[i].RemoveAll(m => m.ActiveHandle == handle);
        Dirty = true;
    }

    public int GetModCount(EGameplayModOp op) => modBuckets[(int)op].Count;

    /// <summary>聚合公式同 UE：Override 优先，否则 ((Base + ΣAdd) × ΠMul / ΠDiv) + ΣFinalAdd。</summary>
    public float Evaluate()
    {
        // Override check
        var overrides = modBuckets[(int)EGameplayModOp.Override];
        if (overrides.Count > 0)
            return overrides[^1].Magnitude; // 最后一个 Override 胜出

        float result = BaseValue;

        // ΣAdd
        foreach (var m in modBuckets[(int)EGameplayModOp.Additive])
            result += m.Magnitude;

        // ΠMultiply
        float mul = 1f;
        foreach (var m in modBuckets[(int)EGameplayModOp.Multiply])
            mul *= m.Magnitude;
        result *= mul;

        // / ΠDivide
        float div = 1f;
        foreach (var m in modBuckets[(int)EGameplayModOp.Divide])
            div *= m.Magnitude;
        if (div != 0f) result /= div;

        // + ΣFinalAdd
        foreach (var m in modBuckets[(int)EGameplayModOp.FinalAdd])
            result += m.Magnitude;

        Dirty = false;
        return result;
    }
}
