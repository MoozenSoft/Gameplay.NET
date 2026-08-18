using Friflo.Engine.ECS.Systems;
using Gameplay.Core;
using Xunit;

namespace Gameplay.Tests.Core;

public class MovementSystemTests
{
    [Fact]
    public void Update_IntegratesVelocityIntoPosition()
    {
        var world = new World(ENetMode.Standalone);
        world.AddSystem(new MovementSystem(), ESimulationStage.Simulation);
        var entity = world.Store.CreateEntity();
        entity.AddComponent(new TransformComponent { Position = default, Scale = 1f });
        entity.AddComponent(new VelocityComponent { Velocity = new Vector3(1f, 0f, 0f) });

        world.Update(0.5f);

        ref var transform = ref entity.GetComponent<TransformComponent>();
        Assert.Equal(0.5f, transform.Position.X, 4);
    }
}
