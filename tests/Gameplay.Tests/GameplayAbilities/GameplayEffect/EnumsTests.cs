// tests/Gameplay.Tests/GameplayAbilities/GameplayEffect/EnumsTests.cs
namespace Gameplay.Tests.GameplayAbilities;

using System;
using Gameplay.GameplayAbilities;
using Xunit;

public class EnumsTests
{
    [Fact]
    public void DurationType_HasExpectedValues()
    {
        Assert.Equal(0, (int)EGameplayEffectDurationType.Instant);
        Assert.Equal(1, (int)EGameplayEffectDurationType.HasDuration);
        Assert.Equal(2, (int)EGameplayEffectDurationType.Infinite);
    }

    [Fact]
    public void ModOp_HasAllOperations()
    {
        var values = Enum.GetValues<EGameplayModOp>();
        Assert.Contains(EGameplayModOp.Additive, values);
        Assert.Contains(EGameplayModOp.Multiply, values);
        Assert.Contains(EGameplayModOp.Divide, values);
        Assert.Contains(EGameplayModOp.Override, values);
        Assert.Contains(EGameplayModOp.FinalAdd, values);
    }

    [Fact]
    public void ModifierExecutionType_HasExpectedValues()
    {
        Assert.Equal(0, (int)ModifierExecutionType.Persistent);
        Assert.Equal(1, (int)ModifierExecutionType.ExecuteOnApply);
        Assert.Equal(2, (int)ModifierExecutionType.ExecuteOnPeriod);
    }
}
