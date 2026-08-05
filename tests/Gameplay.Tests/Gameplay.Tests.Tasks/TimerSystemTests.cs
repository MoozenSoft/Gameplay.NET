// tests/Gameplay.Tests/Gameplay.Tests.Tasks/TimerSystemTests.cs
namespace Gameplay.Tests.Tasks;

using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Gameplay.Abilities;
using Gameplay.Tasks;
using Xunit;

/// <summary>对应 src/Gameplay/Gameplay.Tasks/Systems/TimerSystem.cs——间隔脉冲事件，N 次后完成（Repeat 的底层能力）。</summary>
public class TimerSystemTests
{
    private const ushort PulseEventId = 42;

    private static (Entity Task, GameplayEventBus Bus, SystemRoot Root) Setup(float interval, int remainingPulses)
    {
        var store = new EntityStore();
        var bus = new GameplayEventBus();
        var system = new TimerSystem(bus);

        var task = store.CreateEntity();
        task.AddComponent(new TaskStateComponent { State = ETaskState.Pending });
        task.AddComponent(new TimerComponent
        {
            Interval = interval,
            RemainingPulses = remainingPulses,
            PulseEventId = PulseEventId,
        });

        var root = new SystemRoot(store) { system };
        return (task, bus, root);
    }

    private static ETaskState GetState(Entity task)
        => task.GetComponent<TaskStateComponent>().State;

    [Fact]
    public void EmitsPulseEvent_EachInterval()
    {
        var (task, bus, root) = Setup(interval: 0.32f, remainingPulses: 3);

        root.Update(new UpdateTick(0.16f, 0)); // Pending → Running（不累积）
        root.Update(new UpdateTick(0.16f, 0)); // Elapsed=0.16
        root.Update(new UpdateTick(0.16f, 0)); // Elapsed=0.32 → 脉冲 1

        var frame = bus.Swap();
        Assert.Equal(1, frame.Records.Count);
        Assert.Equal(PulseEventId, frame.Records.GetRef(0).EventId);
    }

    [Fact]
    public void CompletesAfterCountPulses()
    {
        var (task, bus, root) = Setup(interval: 0.32f, remainingPulses: 2);

        root.Update(new UpdateTick(0.16f, 0)); // Pending → Running（不累积）
        root.Update(new UpdateTick(0.16f, 0)); // 0.16
        root.Update(new UpdateTick(0.16f, 0)); // 0.32 → 脉冲 1
        Assert.Equal(ETaskState.Running, GetState(task));

        root.Update(new UpdateTick(0.16f, 0)); // 0.48
        root.Update(new UpdateTick(0.16f, 0)); // 0.64 → 脉冲 2 → 完成

        Assert.Equal(ETaskState.Done, GetState(task));
        Assert.Equal(2, bus.Swap().Records.Count); // 两个脉冲事件都已发出
    }

    [Fact]
    public void MultiplePulses_NoAccumulationError()
    {
        var (task, bus, root) = Setup(interval: 0.5f, remainingPulses: 5);

        root.Update(new UpdateTick(0.16f, 0)); // Pending → Running（不累积）
        root.Update(new UpdateTick(0.16f, 0)); // 0.16
        root.Update(new UpdateTick(0.16f, 0)); // 0.32
        root.Update(new UpdateTick(0.16f, 0)); // 0.48
        root.Update(new UpdateTick(0.16f, 0)); // 0.64 → 脉冲 1，残留 0.14

        var frame1 = bus.Swap();
        Assert.Equal(1, frame1.Records.Count);

        root.Update(new UpdateTick(0.16f, 0)); // 0.30
        root.Update(new UpdateTick(0.16f, 0)); // 0.46
        root.Update(new UpdateTick(0.16f, 0)); // 0.62 → 脉冲 2，残留 0.12

        var frame2 = bus.Swap();
        Assert.Equal(1, frame2.Records.Count); // 无累积误差（残留保留，不丢不重）
    }

    [Fact]
    public void ZeroRemainingPulses_KeepsRunningForever()
    {
        var (task, bus, root) = Setup(interval: 0.32f, remainingPulses: 0);

        root.Update(new UpdateTick(0.16f, 0)); // Pending → Running（不累积）
        root.Update(new UpdateTick(0.16f, 0));
        root.Update(new UpdateTick(0.16f, 0)); // 脉冲 1（无限模式不计数）
        root.Update(new UpdateTick(0.16f, 0));
        root.Update(new UpdateTick(0.16f, 0)); // 脉冲 2

        Assert.Equal(ETaskState.Running, GetState(task)); // 永不完成
        Assert.Equal(2, bus.Swap().Records.Count);
    }
}
