namespace Gameplay.Tests.Abilities;

using Gameplay.Abilities;
using Xunit;

public class AbilityActivationRequestTests
{
    [Fact]
    public void Default_Source_IsInput()
    {
        var req = new AbilityActivationRequest();
        Assert.Equal(EActivationSource.Input, req.Source);
    }

    [Fact]
    public void SpecHandle_DefaultsToZero()
    {
        var req = new AbilityActivationRequest();
        Assert.Equal(0, req.SpecHandle);
    }
}
