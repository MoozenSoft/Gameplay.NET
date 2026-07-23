// tests/Gameplay.Tests/GameplayAbilities/GameplayEffect/GameplayModifierTests.cs
namespace Gameplay.Tests.GameplayAbilities;

using Gameplay.GameplayAbilities;
using Xunit;

public class GameplayModifierTests
{
    [Fact]
    public void ScalableFloat_CreatesMagnitude()
    {
        var mag = GameplayEffectModifierMagnitude.CreateScalableFloat(1.5f, 10f);
        Assert.Equal(EGameplayEffectMagnitudeCalculation.ScalableFloat, mag.CalculationType);
    }

    [Fact]
    public void AttributeBased_CreatesMagnitude()
    {
        var mag = GameplayEffectModifierMagnitude.CreateAttributeBased(
            coefficient: 1.0f, preAdd: 0f, postAdd: 0f);
        Assert.Equal(EGameplayEffectMagnitudeCalculation.AttributeBased, mag.CalculationType);
    }

    [Fact]
    public void Modifier_DefaultCapturePolicy_IsSnapshot()
    {
        var modifier = new GameplayModifier
        {
            ModOp = EGameplayModOp.Additive,
            CapturePolicy = default
        };
        Assert.Equal(EAttributeCapturePolicy.Snapshot, modifier.CapturePolicy);
    }
}
