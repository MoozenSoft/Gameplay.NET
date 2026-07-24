// tests/Gameplay.Tests/GameplayAbilities/GameplayEffect/GameplayEffectSpecTests.cs
namespace Gameplay.Tests.Abilities;

using Xunit;
using Gameplay.Tags;
using Gameplay.Abilities;

public class GameplayEffectSpecTests
{
    static GameplayEffectSpecTests() { GameplayTagManager.RegisterTags("SetByCaller.Damage"); }
    [Fact]
    public void Constructor_SetsDefinition()
    {
        var ge = new GameplayEffect
        {
            DurationPolicy = EGameplayEffectDurationType.HasDuration,
            Period = 1.5f,
        };
        var spec = new GameplayEffectSpec(ge, level: 3f);
        Assert.Same(ge, spec.Definition);
        Assert.Equal(3f, spec.Level);
    }

    [Fact]
    public void StackCount_DefaultsToOne()
    {
        var spec = new GameplayEffectSpec(new GameplayEffect(), 1f);
        Assert.Equal(1, spec.StackCount);
    }

    [Fact]
    public void Duration_ReflectsDefinition()
    {
        var ge = new GameplayEffect
        {
            DurationPolicy = EGameplayEffectDurationType.HasDuration,
        };
        var spec = new GameplayEffectSpec(ge, 1f) { Duration = 5.0f };
        Assert.Equal(5.0f, spec.Duration);
    }

    [Fact]
    public void SetByCallerMagnitude_SetAndGet()
    {
        var tag = GameplayTag.Request("SetByCaller.Damage");
        var spec = new GameplayEffectSpec(new GameplayEffect(), 1f);
        spec.SetSetByCallerMagnitude(tag, 42f);
        Assert.Equal(42f, spec.GetSetByCallerMagnitude(tag, false));
    }
}
