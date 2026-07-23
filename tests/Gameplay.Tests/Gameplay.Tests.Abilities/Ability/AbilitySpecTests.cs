namespace Gameplay.Tests.Abilities;

using Gameplay.Abilities;
using Xunit;

public class AbilitySpecTests
{
    [Fact]
    public void Constructor_SetsFields()
    {
        var ability = new GameplayAbility();
        var spec = new AbilitySpec
        {
            Ability = ability,
            Level = 3,
            InputID = 1,
        };
        Assert.Same(ability, spec.Ability);
        Assert.Equal(3, spec.Level);
        Assert.Equal(1, spec.InputID);
    }

    [Fact]
    public void Default_RemovalPolicy_IsCancelImmediately()
    {
        var spec = new AbilitySpec();
        Assert.Equal(EGrantedAbilityRemovePolicy.CancelAbilityImmediately, spec.RemovalPolicy);
    }

    [Fact]
    public void Collection_StoresSpecs()
    {
        var comp = new AbilityCollectionComponent();
        comp.Specs = new AbilitySpec[]
        {
            new() { Level = 1 },
            new() { Level = 2 },
        };
        Assert.Equal(2, comp.Specs.Length);
    }
}
