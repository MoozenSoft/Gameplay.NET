// tests/Gameplay.Tests/GameplayAbilities/Attribute/AttributeAggregatorTests.cs
namespace Gameplay.Tests.Abilities;

using System;
using Gameplay.Abilities;
using Xunit;

public class AttributeAggregatorTests
{
    [Fact]
    public void Default_BaseValue_IsZero()
    {
        var agg = new AttributeAggregator();
        Assert.Equal(0f, agg.BaseValue);
        Assert.False(agg.Dirty);
    }

    [Fact]
    public void AddMod_ReturnsTrue_AndIncrementsBucket()
    {
        var agg = new AttributeAggregator();
        bool changed = agg.AddMod(1, 10f, EGameplayModOp.Additive);
        Assert.True(changed);
        Assert.Equal(1, agg.GetModCount(EGameplayModOp.Additive));
        // AddMod 不设置 Dirty——由 Manager 负责
        Assert.False(agg.Dirty);
    }

    [Fact]
    public void SetBaseValue_Changed_ReturnsTrue()
    {
        var agg = new AttributeAggregator();
        bool changed = agg.SetBaseValue(100f);
        Assert.True(changed);
        Assert.Equal(100f, agg.BaseValue);
    }

    [Fact]
    public void SetBaseValue_SameValue_ReturnsFalse()
    {
        var agg = new AttributeAggregator();
        agg.SetBaseValue(100f);
        bool changed = agg.SetBaseValue(100f);
        Assert.False(changed);
        Assert.Equal(100f, agg.BaseValue);
    }

    [Fact]
    public void Evaluate_Additive_ReturnsCorrectValue()
    {
        var agg = new AttributeAggregator();
        agg.SetBaseValue(100f);
        agg.AddMod(1, 20f, EGameplayModOp.Additive);
        agg.AddMod(2, 30f, EGameplayModOp.Additive);

        float result = agg.Evaluate();
        Assert.Equal(150f, result); // (100 + 20 + 30)
    }

    [Fact]
    public void Evaluate_Override_IgnoresOtherMods()
    {
        var agg = new AttributeAggregator();
        agg.SetBaseValue(100f);
        agg.AddMod(1, 20f, EGameplayModOp.Additive);
        agg.AddMod(2, 999f, EGameplayModOp.Override);

        float result = agg.Evaluate();
        Assert.Equal(999f, result); // Override wins
    }

    [Fact]
    public void Evaluate_DoesNotClearDirty()
    {
        var agg = new AttributeAggregator();
        agg.SetBaseValue(100f);
        agg.AddMod(1, 20f, EGameplayModOp.Additive);
        agg.Dirty = true; // 模拟 Manager 标记
        agg.Evaluate();
        Assert.True(agg.Dirty); // Dirty 仍为 true——由 Manager 负责清理
    }

    [Fact]
    public void RemoveMod_ByHandle_ReturnsTrueAndClearsMod()
    {
        var agg = new AttributeAggregator();
        agg.SetBaseValue(100f);
        agg.AddMod(1, 20f, EGameplayModOp.Additive);

        bool removed = agg.RemoveModsByHandle(1);
        Assert.True(removed);
        Assert.Equal(0, agg.GetModCount(EGameplayModOp.Additive));
        Assert.Equal(100f, agg.Evaluate()); // back to base
    }

    [Fact]
    public void RemoveMod_NonExistentHandle_ReturnsFalse()
    {
        var agg = new AttributeAggregator();
        agg.AddMod(1, 20f, EGameplayModOp.Additive);

        bool removed = agg.RemoveModsByHandle(999);
        Assert.False(removed);
        Assert.Equal(1, agg.GetModCount(EGameplayModOp.Additive));
    }

    [Fact]
    public void Evaluate_FullFormula()
    {
        // ((Base + Add) * Mul / Div) + FinalAdd
        var agg = new AttributeAggregator();
        agg.SetBaseValue(100f);
        agg.AddMod(1, 20f, EGameplayModOp.Additive);
        agg.AddMod(2, 1.5f, EGameplayModOp.Multiply);
        agg.AddMod(3, 5f, EGameplayModOp.FinalAdd);

        Assert.Equal(185f, agg.Evaluate()); // ((100+20)*1.5/1) + 5
    }
}
