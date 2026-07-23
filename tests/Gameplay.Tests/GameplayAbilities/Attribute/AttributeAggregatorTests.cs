// tests/Gameplay.Tests/GameplayAbilities/Attribute/AttributeAggregatorTests.cs
namespace Gameplay.Tests.GameplayAbilities;

using System;
using Gameplay.GameplayAbilities;
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
    public void AddMod_Dirties_AndIncrementsBucket()
    {
        var agg = new AttributeAggregator();
        agg.AddMod(1, 10f, EGameplayModOp.Additive);
        Assert.True(agg.Dirty);
        Assert.Equal(1, agg.GetModCount(EGameplayModOp.Additive));
    }

    [Fact]
    public void Evaluate_Additive_ReturnsCorrectValue()
    {
        var agg = new AttributeAggregator { BaseValue = 100f };
        agg.AddMod(1, 20f, EGameplayModOp.Additive);
        agg.AddMod(2, 30f, EGameplayModOp.Additive);

        float result = agg.Evaluate();
        Assert.Equal(150f, result); // (100 + 20 + 30)
    }

    [Fact]
    public void Evaluate_Override_IgnoresOtherMods()
    {
        var agg = new AttributeAggregator { BaseValue = 100f };
        agg.AddMod(1, 20f, EGameplayModOp.Additive);
        agg.AddMod(2, 999f, EGameplayModOp.Override);

        float result = agg.Evaluate();
        Assert.Equal(999f, result); // Override wins
    }

    [Fact]
    public void RemoveMod_ByHandle_ClearsMod()
    {
        var agg = new AttributeAggregator { BaseValue = 100f };
        agg.AddMod(1, 20f, EGameplayModOp.Additive);

        agg.RemoveModsByHandle(1);
        Assert.Equal(0, agg.GetModCount(EGameplayModOp.Additive));
        Assert.Equal(100f, agg.Evaluate()); // back to base
    }

    [Fact]
    public void Evaluate_FullFormula()
    {
        // ((Base + Add) * Mul / Div) + FinalAdd
        var agg = new AttributeAggregator { BaseValue = 100f };
        agg.AddMod(1, 20f, EGameplayModOp.Additive);
        agg.AddMod(2, 1.5f, EGameplayModOp.Multiply);
        agg.AddMod(3, 5f, EGameplayModOp.FinalAdd);

        Assert.Equal(185f, agg.Evaluate()); // ((100+20)*1.5/1) + 5
    }
}
