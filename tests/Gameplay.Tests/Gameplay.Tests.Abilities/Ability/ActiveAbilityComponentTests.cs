// tests/Gameplay.Tests/Gameplay.Tests.Abilities/Ability/ActiveAbilityComponentTests.cs
namespace Gameplay.Tests.Abilities;

using Gameplay.Abilities;
using Xunit;

public class ActiveAbilityComponentTests
{
    [Fact]
    public void Default_State_IsActivating()
    {
        var comp = new ActiveAbilityComponent();
        Assert.Equal(AbilityInstanceState.Activating, comp.State);
    }

    [Fact]
    public void Default_Handle_IsZero()
    {
        var comp = new ActiveAbilityComponent();
        Assert.Equal(0, comp.Handle);
    }

    [Fact]
    public void Default_IsActive_IsFalse()
    {
        var comp = new ActiveAbilityComponent();
        Assert.False(comp.IsActive);
    }
}
