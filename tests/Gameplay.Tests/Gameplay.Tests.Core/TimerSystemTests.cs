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

    [Fact]
    public void Update_LoopTimer_KeepsCompletedOneFrame_ThenResets()
    {
        var world = new World(ENetMode.Standalone);
        world.AddSystem(new TimerSystem(), ESimulationStage.Simulation);
        var entity = world.Store.CreateEntity();
        entity.AddComponent(new TimerComponent { Remaining = 0.5f, Duration = 0.5f, Loop = true });

        world.Update(1.0f);

        // 第一帧到期：Completed 保持一帧，供消费方观察「一圈完成」
        Assert.True(entity.GetComponent<TimerComponent>().Completed);

        world.Update(1.0f);

        // 第二帧：已进入下一圈，Completed 复位、Remaining 为正
        var timer = entity.GetComponent<TimerComponent>();
        Assert.False(timer.Completed);
        Assert.True(timer.Remaining > 0f);
    }
}
