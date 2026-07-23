// tests/Gameplay.Tests/Gameplay.Tests.Abilities/Ability/AbilityActivationSystemTests.cs
namespace Gameplay.Tests.Abilities;

using Friflo.Engine.ECS;
using Gameplay.Abilities;
using Gameplay.Tags;
using Xunit;

public class AbilityActivationSystemTests
{
    [Fact]
    public void TryActivate_NoAbilityCollection_ReturnsFalse()
    {
        var store = new EntityStore();
        var attrSys = new AttributeSystem();
        var effectSys = new EffectSystem(attrSys);
        var sys = new AbilityActivationSystem(effectSys);

        var owner = store.CreateEntity();
        var request = new AbilityActivationRequest { Owner = owner, SpecHandle = 0 };

        Assert.False(sys.TryActivateAbility(request));
    }

    [Fact]
    public void TryActivate_InvalidSpecHandle_ReturnsFalse()
    {
        var store = new EntityStore();
        var attrSys = new AttributeSystem();
        var effectSys = new EffectSystem(attrSys);
        var sys = new AbilityActivationSystem(effectSys);

        var owner = store.CreateEntity();
        owner.AddComponent(new AbilityCollectionComponent
        {
            Specs = new AbilitySpec[] { new() { Level = 1 } }
        });
        var request = new AbilityActivationRequest { Owner = owner, SpecHandle = 99 };

        Assert.False(sys.TryActivateAbility(request));
    }

    [Fact]
    public void TryActivate_RequirementsFail_ReturnsFalse()
    {
        GameplayTagManager.RegisterTags("State.Stunned");

        var store = new EntityStore();
        var attrSys = new AttributeSystem();
        var effectSys = new EffectSystem(attrSys);

        var blockedTag = GameplayTag.Request("State.Stunned");
        var ability = new GameplayAbility();
        ability.ActivationBlockedTags.AddTag(blockedTag);

        var sys = new AbilityActivationSystem(effectSys);
        var owner = store.CreateEntity();
        owner.AddComponent(new GameplayTagsComponent());
        owner.GetComponent<GameplayTagsComponent>().AddTag(blockedTag);
        owner.AddComponent(new AbilityCollectionComponent
        {
            Specs = new[] { new AbilitySpec { Ability = ability, Handle = 0 } }
        });

        var request = new AbilityActivationRequest { Owner = owner, SpecHandle = 0 };
        Assert.False(sys.TryActivateAbility(request));
    }

    [Fact]
    public void TryActivate_Success_CreatesActiveAbilityEntity()
    {
        var store = new EntityStore();
        var attrSys = new AttributeSystem();
        var effectSys = new EffectSystem(attrSys);
        var sys = new AbilityActivationSystem(effectSys);

        var ability = new GameplayAbility();
        var executor = new TestExecutor();
        ability.Executor = executor;

        var owner = store.CreateEntity();
        owner.AddComponent(new AbilityCollectionComponent
        {
            Specs = new[] { new AbilitySpec { Ability = ability, Handle = 0 } }
        });

        var request = new AbilityActivationRequest { Owner = owner, SpecHandle = 0 };
        Assert.True(sys.TryActivateAbility(request));
        Assert.True(executor.WasCalled);
    }

    private class TestExecutor : IAbilityExecutor
    {
        public bool WasCalled;
        public void Execute(Entity activeAbilityEntity, in AbilityActivationRequest request)
            => WasCalled = true;
    }
}
