// tests/Gameplay.Tests/GameplayAbilities/GameplayAbilitiesFeatureTests.cs
namespace Gameplay.Tests.Abilities;

using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Gameplay;
using Gameplay.Abilities;
using Xunit;

public class GameplayAbilitiesFeatureTests
{
    [Fact]
    public void Constructor_RegistersAllSystems()
    {
        var world = new World(NetMode.Standalone);
        var feature = new GameplayAbilitiesFeature(world.Store, world.NetMode);

        Assert.NotNull(feature.EffectSystem);
        Assert.NotNull(feature.AttributeAggregatorManager);
        Assert.NotNull(feature.SystemRoot);
        Assert.NotNull(feature.EventBus);
        Assert.NotNull(feature.EventDispatcher);
        Assert.NotNull(feature.ActivationManager);
        Assert.NotNull(feature.AbilityTaskSystem);
        Assert.NotNull(feature.PredictionManager);
        Assert.NotNull(feature.CueManager);
    }

    [Fact]
    public void Update_ExecutesSystems()
    {
        var store = new EntityStore();
        var feature = new GameplayAbilitiesFeature(store, NetMode.Standalone);

        var entity = store.CreateEntity();
        feature.AttributeAggregatorManager.SetAggregatorValue(entity, new GameplayAttribute(0), 100f);
        feature.AttributeAggregatorManager.AddAggregatorMod(entity, new GameplayAttribute(0), geHandle: new GameplayEffectHandle(1), magnitude: 20f, EGameplayModOp.Additive);

        // Update 不抛异常即通过
        feature.Update(0.016f);
    }
}
