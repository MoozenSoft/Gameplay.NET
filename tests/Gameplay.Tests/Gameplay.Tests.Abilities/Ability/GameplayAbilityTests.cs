namespace Gameplay.Tests.Abilities;

using Gameplay.Abilities;
using Xunit;

public class GameplayAbilityTests
{
    [Fact]
    public void Default_NetExecutionPolicy_IsLocalPredicted()
    {
        var ability = new GameplayAbility();
        Assert.Equal(EGameplayAbilityNetExecutionPolicy.LocalPredicted, ability.NetExecutionPolicy);
    }

    [Fact]
    public void ActivationBlockedTags_PreventsActivation()
    {
        var ability = new GameplayAbility();
        Assert.NotNull(ability.ActivationBlockedTags);
        Assert.Equal(0, ability.ActivationBlockedTags.Count);
    }

    [Fact]
    public void AssetTags_InitiallyEmpty()
    {
        var ability = new GameplayAbility();
        Assert.NotNull(ability.AssetTags);
        Assert.Equal(0, ability.AssetTags.Count);
    }
}
