namespace Gameplay.Tests.Abilities;

using Gameplay.Abilities;
using Gameplay.Tags;
using Xunit;

public class GameplayEffectQueryTests
{
    static GameplayEffectQueryTests() { GameplayTagManager.RegisterTags("Buff.Fire", "Buff.Ice", "Test.Poison", "Test.Fire"); }
    [Fact]
    public void MatchByDefinition_Matches()
    {
        var ge = new GameplayEffect { DurationPolicy = EGameplayEffectDurationType.HasDuration };
        var spec = new GameplayEffectSpec(ge, 1f);
        var query = GameplayEffectQuery.MakeQuery_MatchDefinition(ge);

        Assert.True(query.Matches(spec));
    }

    [Fact]
    public void MatchByTag_NonMatching_ReturnsFalse()
    {
        GameplayTagManager.RegisterTags("Buff.Fire", "Buff.Ice");
        var ge = new GameplayEffect();
        ge.GrantedTags.AddTag(GameplayTag.Request("Buff.Fire"));
        var spec = new GameplayEffectSpec(ge, 1f);

        var requiredTag = GameplayTag.Request("Buff.Ice");
        var query = GameplayEffectQuery.MakeQuery_MatchAnyGrantedTags(
            new GameplayTagContainer { requiredTag });

        Assert.False(query.Matches(spec));
    }

    [Fact]
    public void MatchByEffectTag_Matching_ReturnsTrue()
    {
        var ge = new GameplayEffect { AssetTags = new GameplayTagContainer { GameplayTag.Request("Test.Poison") } };
        var spec = new GameplayEffectSpec(ge, 1f);
        var query = new GameplayEffectQuery { EffectTagQuery = new GameplayTagContainer { GameplayTag.Request("Test.Poison") } };

        Assert.True(query.Matches(spec));
    }

    [Fact]
    public void MatchByEffectTag_NonMatching_ReturnsFalse()
    {
        var ge = new GameplayEffect { AssetTags = new GameplayTagContainer { GameplayTag.Request("Test.Poison") } };
        var spec = new GameplayEffectSpec(ge, 1f);
        var query = new GameplayEffectQuery { EffectTagQuery = new GameplayTagContainer { GameplayTag.Request("Test.Fire") } };

        Assert.False(query.Matches(spec));
    }

    [Fact]
    public void Empty_MatchesAnything()
    {
        var spec = new GameplayEffectSpec(new GameplayEffect(), 1f);
        var query = new GameplayEffectQuery();
        Assert.True(query.IsEmpty);
        Assert.True(query.Matches(spec));
    }
}
