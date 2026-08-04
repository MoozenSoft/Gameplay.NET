// tests/Gameplay.Tests/Gameplay.Tests.Tasks/GameplayEventSystemTests.cs
namespace Gameplay.Tests.Tasks;

using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Gameplay.Abilities;
using Gameplay.Tasks;
using Xunit;

/// <summary>对应 src/Gameplay/Gameplay.Tasks/Systems/GameplayEventSystem.cs——完整系统链：注册 → 事件分发 → Done。</summary>
public class GameplayEventSystemTests
{
    private static (Entity Task, GameplayEventBus Bus, GameplayEventDispatcher Dispatcher, SystemRoot Root) Setup(ushort eventId)
    {
        var store = new EntityStore();
        var bus = new GameplayEventBus();
        var dispatcher = new GameplayEventDispatcher(bus);

        var task = TaskBuilder.WaitEvent(store, eventId, owner: store.CreateEntity());

        var root = new SystemRoot(store) { new GameplayEventSystem(dispatcher, store) };
        return (task, bus, dispatcher, root);
    }

    private static ETaskState GetState(Entity task)
        => task.GetComponent<TaskStateComponent>().State;

    [Fact]
    public void MatchingEvent_CompletesTask()
    {
        const ushort eventId = 5;
        var (task, bus, dispatcher, root) = Setup(eventId);

        root.Update(new UpdateTick(0.16f, 0)); // Pending → 注册动态 Listener → Running
        Assert.Equal(ETaskState.Running, GetState(task));

        bus.Enqueue(new GameplayEventRecord { EventId = eventId, Magnitude = 10f });
        dispatcher.Tick(); // Phase 0: 消费事件 → 通知动态 Listener → Task 设 Done
        root.Update(new UpdateTick(0.16f, 0)); // System 检测到 Done

        Assert.Equal(ETaskState.Done, GetState(task));
    }

    [Fact]
    public void NonMatchingEvent_DoesNotCompleteTask()
    {
        const ushort eventId = 5;
        var (task, bus, dispatcher, root) = Setup(eventId);

        root.Update(new UpdateTick(0.16f, 0)); // 注册 → Running

        bus.Enqueue(new GameplayEventRecord { EventId = 99, Magnitude = 10f });
        dispatcher.Tick();
        root.Update(new UpdateTick(0.16f, 0));

        Assert.Equal(ETaskState.Running, GetState(task));
    }

    [Fact]
    public void CompletedTask_IsUnregistered()
    {
        const ushort eventId = 5;
        var (task, bus, dispatcher, root) = Setup(eventId);

        root.Update(new UpdateTick(0.16f, 0)); // 注册 → Running

        bus.Enqueue(new GameplayEventRecord { EventId = eventId });
        dispatcher.Tick();
        root.Update(new UpdateTick(0.16f, 0)); // Done，System 注销 Listener
        Assert.Equal(ETaskState.Done, GetState(task));

        // 注销后再收到同 ID 事件，不再被通知
        bus.Enqueue(new GameplayEventRecord { EventId = eventId });
        dispatcher.Tick();
        root.Update(new UpdateTick(0.16f, 0));

        ref var state = ref task.GetComponent<TaskStateComponent>();
        Assert.Equal(ETaskState.Done, state.State); // 仍为 Done（无副作用）
    }
}
