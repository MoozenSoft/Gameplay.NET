namespace Gameplay.Tests.Abilities;

using Friflo.Engine.ECS;
using Gameplay.Abilities;
using Gameplay.Tags;
using Xunit;

public class ApplyCooldownCommitTests
{
    [Fact]
    public void Execute_NoCooldownEffect_Skips()
    {
        var store = new EntityStore();
        var attrSys = new AttributeSystem();
        var effectSys = new EffectSystem(attrSys);
        var commit = new ApplyCooldownCommit(effectSys);
        var owner = store.CreateEntity();

        var ability = new GameplayAbility(); // CooldownEffect = null
        var spec = new AbilitySpec { Ability = ability, Handle = 1 };
        var request = new AbilityActivationRequest { Owner = owner, SpecHandle = 1 };

        // 不应抛出异常
        commit.Execute(owner, spec, request);
    }
}

public class TagRequirementTests
{
    [Fact]
    public void Evaluate_NoRequirements_ReturnsTrue()
    {
        var req = new TagRequirement();
        var store = new EntityStore();
        var owner = store.CreateEntity();
        var spec = new AbilitySpec();
        var request = new AbilityActivationRequest { Owner = owner };

        Assert.True(req.Evaluate(owner, spec, request));
    }

    [Fact]
    public void Evaluate_ActivationBlocked_Fails()
    {
        var blockedTag = GameplayTag.Request("State.Dead");
        var ability = new GameplayAbility();
        ability.ActivationBlockedTags.AddTag(blockedTag);

        var store = new EntityStore();
        var owner = store.CreateEntity();
        owner.AddComponent(new GameplayTagsComponent());
        owner.GetComponent<GameplayTagsComponent>().AddTag(blockedTag);

        var spec = new AbilitySpec { Ability = ability };
        var request = new AbilityActivationRequest { Owner = owner };
        var req = new TagRequirement();

        Assert.False(req.Evaluate(owner, spec, request));
    }
}
