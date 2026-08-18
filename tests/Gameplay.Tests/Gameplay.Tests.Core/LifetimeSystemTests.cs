using Gameplay.Core;
using Xunit;

namespace Gameplay.Tests.Core;

public class LifetimeSystemTests
{
    [Fact]
    public void Update_ExpiredLifetime_DeletesEntity()
    {
        var world = new World(ENetMode.Standalone);
        world.AddSystem(new LifetimeSystem(), ESimulationStage.Simulation);
        var entity = world.Store.CreateEntity();
        entity.AddComponent(new LifetimeComponent { Remaining = 0.5f });

        world.Update(1f);

        Assert.True(world.Store.GetEntityById(entity.Id).IsNull);
    }
}
