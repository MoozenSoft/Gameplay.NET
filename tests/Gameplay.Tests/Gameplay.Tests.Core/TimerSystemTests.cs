using Gameplay.Core;
using Xunit;

namespace Gameplay.Tests.Core;

public class TimerSystemTests
{
    [Fact]
    public void Update_DecrementsRemaining_AndSetsCompleted()
    {
        var world = new World(ENetMode.Standalone);
        world.AddSystem(new TimerSystem(), ESimulationStage.Simulation);
        var entity = world.Store.CreateEntity();
        entity.AddComponent(new TimerComponent { Remaining = 1f, Duration = 1f });

        world.Update(1.5f);

        ref var timer = ref entity.GetComponent<TimerComponent>();
        Assert.True(timer.Completed);
        Assert.True(timer.Remaining <= 0f);
    }
}
