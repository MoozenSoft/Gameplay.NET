using System;

namespace Gameplay.Core;

/// <summary>确定性随机（SplitMix64，跨平台一致）。</summary>
public sealed class DeterministicRng
{
    private ulong state;

    public DeterministicRng(ulong seed) => state = seed;

    /// <summary>当前内部状态（快照/回放）。</summary>
    public ulong State => state;

    /// <summary>下一个无符号 32 位随机数。</summary>
    public uint NextUInt()
    {
        state += 0x9E3779B97F4A7C15UL;
        ulong z = state;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        z ^= z >> 31;
        return (uint)(z & 0xFFFFFFFF);
    }

    /// <summary>[0,1) 区间的随机浮点数。</summary>
    public float NextFloat() => (NextUInt() >> 8) * (1f / 16777216f);

    /// <summary>[minInclusive, maxExclusive) 区间的随机整数。</summary>
    public int Range(int minInclusive, int maxExclusive)
    {
        if (maxExclusive <= minInclusive)
            throw new ArgumentOutOfRangeException(nameof(maxExclusive), "maxExclusive 必须大于 minInclusive");
        var range = (uint)(maxExclusive - minInclusive);
        return minInclusive + (int)(NextUInt() % range);
    }

    /// <summary>派生独立流（per-entity / per-system）。</summary>
    public DeterministicRng Fork(int streamId)
        => new(state ^ (ulong)(streamId) * 0x9E3779B97F4A7C15UL);
}
