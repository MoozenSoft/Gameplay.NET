// tests/Gameplay.Tests/GameplayAbilities/GameplayEffect/ActiveGameplayEffectComponentTests.cs
namespace Gameplay.Tests.GameplayAbilities;

using Gameplay.GameplayAbilities;
using Xunit;

public class ActiveGameplayEffectComponentTests
{
    [Fact]
    public void Default_StackCount_IsZero()
    {
        var comp = new ActiveGameplayEffectComponent();
        Assert.Equal(0, comp.StackCount);
    }

    [Fact]
    public void Default_IsNotInhibited()
    {
        var comp = new ActiveGameplayEffectComponent();
        Assert.False(comp.IsInhibited);
    }

    [Fact]
    public void Default_Duration_IsZero()
    {
        var comp = new ActiveGameplayEffectComponent();
        Assert.Equal(0f, comp.Duration);
    }
}
