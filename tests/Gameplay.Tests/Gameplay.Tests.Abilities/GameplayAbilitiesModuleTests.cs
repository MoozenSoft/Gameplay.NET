// tests/Gameplay.Tests/Gameplay.Tests.Abilities/GameplayAbilitiesModuleTests.cs
namespace Gameplay.Tests.Abilities;

using Gameplay.Abilities;
using Gameplay.Core;
using Xunit;

public class GameplayAbilitiesModuleTests
{
    [Fact]
    public void Build_RegistersAllSystems()
    {
        var world = new World(ENetMode.Standalone);
        var module = new GameplayAbilitiesModule(world);
        world.AddModule(module);

        Assert.NotNull(module.EffectSystem);
        Assert.NotNull(module.AttributeAggregatorManager);
        Assert.NotNull(module.EventBus);
        Assert.NotNull(module.EventDispatcher);
        Assert.NotNull(module.ActivationManager);
        Assert.NotNull(module.TaskScheduler);
        Assert.NotNull(module.PredictionManager);
#if GP_SERVER
        Assert.Null(module.CueManager);    // Server 编译：DS 无表现层，CueManager 为 null
#else
        Assert.NotNull(module.CueManager);
#endif
    }

    [Fact]
    public void Update_ExecutesSystems()
    {
        var world = new World(ENetMode.Standalone);
        var module = new GameplayAbilitiesModule(world);
        world.AddModule(module);

        var entity = world.Store.CreateEntity();
        module.AttributeAggregatorManager.SetAggregatorValue(entity, new GameplayAttribute(0), 100f);
        module.AttributeAggregatorManager.AddAggregatorMod(entity, new GameplayAttribute(0), geHandle: new GameplayEffectHandle(1), magnitude: 20f, EGameplayModOp.Additive);

        // World 统一驱动（三阶段调度），Update 不抛异常即通过
        world.Update(0.016f);
    }
}
