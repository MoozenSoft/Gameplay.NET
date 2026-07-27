// src/Gameplay/GameplayAbilities/Attribute/AttributeAggregator.cs
using System.Collections.Generic;

namespace Gameplay.Abilities;

/// <summary>
/// 单个 GameplayAttribute 的运行时聚合器。
/// 不是 Component，由 AttributeAggregatorManager 内部 Dictionary 管理。
/// Dirty 状态由 Manager 统一控制，Evaluate() 不自行清理。
/// </summary>
internal class AttributeAggregator
{
    internal float BaseValue { get; private set; }
    internal bool   Dirty     { get; set; }

    // ModBuckets[(int)EGameplayModOp] — 按 ModOp 分桶
    private readonly List<ModEntry>[] modBuckets;

    public AttributeAggregator()
    {
        int opCount = System.Enum.GetValues(typeof(EGameplayModOp)).Length;
        modBuckets = new List<ModEntry>[opCount];
        for (int i = 0; i < opCount; i++)
            modBuckets[i] = new List<ModEntry>();
    }

    /// <summary>设置 BaseValue。值真实改变时返回 true。</summary>
    internal bool SetBaseValue(float value)
    {
        if (BaseValue == value) return false;
        BaseValue = value;
        return true;
    }

    /// <summary>添加 Modifier。始终返回 true（总是会改变聚合结果）。</summary>
    internal bool AddMod(int handle, float magnitude, EGameplayModOp op)
    {
        modBuckets[(int)op].Add(new ModEntry { ActiveHandle = handle, Magnitude = magnitude });
        return true;
    }

    /// <summary>按 handle 移除 Modifier。手写双指针就地压缩（零 GC）。实际移除时返回 true。</summary>
    internal bool RemoveModsByHandle(int handle)
    {
        bool removed = false;
        for (int i = 0; i < modBuckets.Length; i++)
        {
            var bucket = modBuckets[i];
            int writeIdx = 0;
            for (int readIdx = 0; readIdx < bucket.Count; readIdx++)
            {
                if (bucket[readIdx].ActiveHandle != handle)
                    bucket[writeIdx++] = bucket[readIdx];
            }
            if (writeIdx < bucket.Count)
            {
                bucket.RemoveRange(writeIdx, bucket.Count - writeIdx);
                removed = true;
            }
        }
        return removed;
    }

    internal int GetModCount(EGameplayModOp op) => modBuckets[(int)op].Count;

    /// <summary>聚合公式同 UE：Override 优先，否则 ((Base + ΣAdd) × ΠMul / ΠDiv) + ΣFinalAdd。不清理 Dirty——由 Manager 负责。</summary>
    internal float Evaluate()
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

        return result;
    }
}
