// tests/Gameplay.Tests/GameplayAbilities/GameplayEffect/GameplayEffectTests.cs
namespace Gameplay.Tests.Abilities;

using Gameplay.Tags;
using Gameplay.Abilities;
using Xunit;

public class GameplayEffectTests
{
    [Fact]
    public void Default_DurationPolicy_IsInstant()
    {
        var ge = new GameplayEffect();
        Assert.Equal(EGameplayEffectDurationType.Instant, ge.DurationPolicy);
    }

    [Fact]
    public void HasDuration_Period_DefaultZero()
    {
        var ge = new GameplayEffect
        {
            DurationPolicy = EGameplayEffectDurationType.HasDuration,
            Period = 2.0f,
        };
        Assert.Equal(EGameplayEffectDurationType.HasDuration, ge.DurationPolicy);
        Assert.Equal(2.0f, ge.Period);
    }

    [Fact]
    public void Modifiers_InitiallyEmpty()
    {
        var ge = new GameplayEffect();
        Assert.NotNull(ge.Modifiers);
        Assert.Empty(ge.Modifiers);
    }

    [Fact]
    public void AddModifier_IncreasesCount()
    {
        var ge = new GameplayEffect();
        ge.Modifiers.Add(new GameplayModifier
        {
            AttributeId = 1,
            ModOp = EGameplayModOp.Additive,
            MagnitudeCalc = GameplayEffectModifierMagnitude.CreateScalableFloat(1f, 10f),
        });
        Assert.Single(ge.Modifiers);
    }
}
