// tests/Gameplay.Tests/GameplayAbilities/Attribute/DirtyAttributeComponentTests.cs
namespace Gameplay.Tests.GameplayAbilities;

using System;
using Gameplay.GameplayAbilities;
using Xunit;

public class DirtyAttributeComponentTests
{
    [Fact]
    public void Default_AllBitsCleared()
    {
        var dc = new DirtyAttributeComponent();
        Assert.Equal(0UL, dc.DirtyBits);
    }

    [Fact]
    public void SetBit_MarksSingleAttribute()
    {
        var dc = new DirtyAttributeComponent();
        dc.SetBit(3);
        Assert.NotEqual(0UL, dc.DirtyBits);
        Assert.True(dc.HasBit(3));
    }

    [Fact]
    public void HasBit_BitNotSet_ReturnsFalse()
    {
        var dc = new DirtyAttributeComponent();
        Assert.False(dc.HasBit(5));
    }

    [Fact]
    public void ClearAll_ResetsToZero()
    {
        var dc = new DirtyAttributeComponent();
        dc.SetBit(0);
        dc.SetBit(10);
        dc.ClearAll();
        Assert.Equal(0UL, dc.DirtyBits);
    }
}
