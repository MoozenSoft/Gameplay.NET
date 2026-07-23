namespace Gameplay.Tests.Abilities;

using Gameplay.Tags;
using Gameplay.Abilities;
using Xunit;

public class GameplayEffectQueryTests
{
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
    public void Empty_MatchesAnything()
    {
        var spec = new GameplayEffectSpec(new GameplayEffect(), 1f);
        var query = new GameplayEffectQuery();
        Assert.True(query.IsEmpty);
        Assert.True(query.Matches(spec));
    }
}
