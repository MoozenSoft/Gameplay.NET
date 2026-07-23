// tests/Gameplay.Tests/Gameplay.Tests.Abilities/Ability/EnumsTests.cs
namespace Gameplay.Tests.Abilities;

using System;
using Gameplay.Abilities;
using Xunit;

public class AbilityEnumsTests
{
    [Fact]
    public void NetExecutionPolicy_HasExpectedValues()
    {
        Assert.Equal(0, (int)EGameplayAbilityNetExecutionPolicy.LocalPredicted);
        Assert.Equal(1, (int)EGameplayAbilityNetExecutionPolicy.LocalOnly);
        Assert.Equal(2, (int)EGameplayAbilityNetExecutionPolicy.ServerInitiated);
        Assert.Equal(3, (int)EGameplayAbilityNetExecutionPolicy.ServerOnly);
    }

    [Fact]
    public void NetSecurityPolicy_HasExpectedValues()
    {
        Assert.Equal(0, (int)EGameplayAbilityNetSecurityPolicy.ClientOrServer);
        Assert.Equal(1, (int)EGameplayAbilityNetSecurityPolicy.ServerOnlyExecution);
        Assert.Equal(2, (int)EGameplayAbilityNetSecurityPolicy.ServerOnlyTermination);
        Assert.Equal(3, (int)EGameplayAbilityNetSecurityPolicy.ServerOnly);
    }

    [Fact]
    public void ActivationSource_HasExpectedValues()
    {
        var values = (EActivationSource[])Enum.GetValues(typeof(EActivationSource));
        Assert.Contains(EActivationSource.Input, values);
        Assert.Contains(EActivationSource.GameplayEvent, values);
    }
}
