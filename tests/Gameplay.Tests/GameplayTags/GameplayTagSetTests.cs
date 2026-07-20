using System;
using Gameplay.GameplayTags;
using Xunit;

namespace Gameplay.Tests.GameplayTags;

public class GameplayTagSetTests
{
    [Fact]
    public void Set_Has_ReturnsTrue_AfterSet()
    {
        var set = new GameplayTagSet();
        set.Set(1);
        Assert.True(set.Has(1));
        Assert.False(set.Has(2));
    }

    [Fact]
    public void Clear_Has_ReturnsFalse_AfterClear()
    {
        var set = new GameplayTagSet();
        set.Set(1);
        set.Clear(1);
        Assert.False(set.Has(1));
    }

    [Fact]
    public void Count_ReturnsCorrectCount()
    {
        var set = new GameplayTagSet();
        Assert.Equal(0, set.Count);
        set.Set(1);
        Assert.Equal(1, set.Count);
        set.Set(3);
        Assert.Equal(2, set.Count);
        set.Set(1); // 重复置位不增 count
        Assert.Equal(2, set.Count);
        set.Clear(1);
        Assert.Equal(1, set.Count);
    }

    [Fact]
    public void HasAny_WithExpandedSet_ReturnsTrue_WhenIntersectionExists()
    {
        var set = new GameplayTagSet();
        set.Set(2); // Damage.Fire
        set.Set(3); // Damage.Ice

        // Damage 的展开集 = {1, 2, 3}
        long[] expanded = new long[1];
        expanded[0] = (1L << 1) | (1L << 2) | (1L << 3);

        Assert.True(set.HasAny(((ReadOnlySpan<long>)expanded)));
    }

    [Fact]
    public void HasAny_WithExpandedSet_ReturnsFalse_WhenNoIntersection()
    {
        var set = new GameplayTagSet();
        set.Set(5); // Buff.Regeneration

        // Damage 的展开集 = {1, 2, 3}
        long[] expanded = new long[1];
        expanded[0] = (1L << 1) | (1L << 2) | (1L << 3);

        Assert.False(set.HasAny(((ReadOnlySpan<long>)expanded)));
    }

    [Fact]
    public void LargeId_ExpandsArrayAndWorks()
    {
        var set = new GameplayTagSet();
        // id=100 → index=1 (100/64), bit=36 (100%64)
        set.Set(100);
        Assert.True(set.Has(100));
        Assert.Equal(1, set.Count);
    }

    [Fact]
    public void HasAny_BetweenTwoSets_ReturnsCorrectResult()
    {
        var a = new GameplayTagSet();
        a.Set(1);
        a.Set(2);

        var b = new GameplayTagSet();
        b.Set(2);
        b.Set(3);

        Assert.True(a.HasAny(b));
    }

    [Fact]
    public void HasAll_ReturnsTrue_WhenAllBitsPresent()
    {
        var a = new GameplayTagSet();
        a.Set(1);
        a.Set(2);
        a.Set(3);

        var b = new GameplayTagSet();
        b.Set(1);
        b.Set(3);

        Assert.True(a.HasAll(b));
    }

    [Fact]
    public void HasAll_ReturnsFalse_WhenBitsMissing()
    {
        var a = new GameplayTagSet();
        a.Set(1);

        var b = new GameplayTagSet();
        b.Set(1);
        b.Set(2);

        Assert.False(a.HasAll(b));
    }
}
