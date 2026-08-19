using Gameplay.Core;
using Xunit;

namespace Gameplay.Tests.Core;

public class WorldLifecycleTests
{
    private sealed class MovementModule : IModule
    {
        public MovementModule(World world)
            => world.AddSystem(new MovementSystem(), ESimulationStage.Simulation);
    }

    [Fact]
    public void IndependentRun_MovementAdvancesWithoutGAS()
    {
        // 独立运行验证：Core 不带 GAS 可跑一个纯 ECS 世界
        var world = new World(ENetMode.Standalone);
        world.AddModule(new MovementModule(world));

        var entity = world.Store.CreateEntity();
        entity.AddComponent(new TransformComponent { Scale = 1f });
        entity.AddComponent(new VelocityComponent { Velocity = new Vector3(2f, 0f, 0f) });

        world.Update(0.16f);
        world.Update(0.16f);

        ref var transform = ref entity.GetComponent<TransformComponent>();
        Assert.Equal(0.64f, transform.Position.X, 4);
        Assert.Equal(2, world.Time.Tick);
    }

    [Fact]
    public void World_HasTimeEventsRandom()
    {
        var world = new World(ENetMode.Standalone);
        Assert.NotNull(world.Time);
        Assert.NotNull(world.Events);
        Assert.NotNull(world.Random);
    }
}
