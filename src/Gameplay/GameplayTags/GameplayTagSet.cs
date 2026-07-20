using System;

namespace Gameplay.GameplayTags;

/// <summary>内部可扩展位集，用 long[] 存储任意数量的 Tag 位。</summary>
internal struct GameplayTagSet
{
    private long[] bits;   // null = 空集（延迟分配）
    private int    count;  // 已设置的 bit 数量

    public int Count => count;

    internal ReadOnlySpan<long> Bits => bits;

    public void Set(int id)
    {
        int index = id >> 6;          // id / 64
        long mask = 1L << (id & 63);  // id % 64
        EnsureCapacity(index);
        if ((bits[index] & mask) == 0)
        {
            count++;
        }
        bits[index] |= mask;
    }

    public void Clear(int id)
    {
        if (bits == null) return;
        int index = id >> 6;
        long mask = 1L << (id & 63);
        if (index < bits.Length && (bits[index] & mask) != 0)
        {
            count--;
            bits[index] &= ~mask;
        }
    }

    public bool Has(int id)
    {
        if (bits == null) return false;
        int index = id >> 6;
        if (index >= bits.Length) return false;
        long mask = 1L << (id & 63);
        return (bits[index] & mask) != 0;
    }

    public bool HasAny(in GameplayTagSet other)
        => HasAny(other.Bits);

    public bool HasAny(ReadOnlySpan<long> expandedSet)
    {
        if (bits == null || expandedSet.Length == 0) return false;
        int minLen = Math.Min(bits.Length, expandedSet.Length);
        for (int i = 0; i < minLen; i++)
        {
            if ((bits[i] & expandedSet[i]) != 0) return true;
        }
        return false;
    }

    public bool HasAll(in GameplayTagSet other)
    {
        if (other.bits == null) return true;
        if (bits == null) return false;
        if (other.bits.Length > bits.Length) return false;
        for (int i = 0; i < other.bits.Length; i++)
        {
            if ((bits[i] & other.bits[i]) != other.bits[i]) return false;
        }
        return true;
    }

    private void EnsureCapacity(int index)
    {
        if (bits == null)
        {
            bits = new long[index + 1];
            return;
        }
        if (index >= bits.Length)
        {
            int newSize = index + 1;
            Array.Resize(ref bits, newSize);
        }
    }
}
