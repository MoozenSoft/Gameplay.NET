using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Gameplay;
using Gameplay.GameplayTasks;
using Xunit;

namespace Gameplay.Tests.GameplayTasks;

public class GameplayTaskTests
{
    private static (World World, SystemRoot Root) Setup()
    {
        var world = new World(NetMode.Standalone);
        var root = new SystemRoot(world.Store) {
            new DelayTaskSystem(),
        };
        return (world, root);
    }

    private static Entity CreateDelayTask(EntityStore store, float duration)
    {
        var entity = store.CreateEntity();
        entity.AddComponent(new TaskOwnerComponent { Owner = default });
        entity.AddComponent(new TaskStateComponent { State = TaskState.Pending });
        entity.AddComponent(new DelayTaskComponent { Duration = duration, Elapsed = 0f });
        return entity;
    }

    [Fact]
    public void PendingTask_TransitionsToRunning_OnUpdate()
    {
        var (world, root) = Setup();
        var entity = CreateDelayTask(world.Store, 3f);

        root.Update(new UpdateTick(0.16f, 0));

        ref var state = ref entity.GetComponent<TaskStateComponent>();
        Assert.Equal(TaskState.Running, state.State);
    }

    [Fact]
    public void RunningTask_IncrementsElapsed_OnUpdate()
    {
        var (world, root) = Setup();
        var entity = CreateDelayTask(world.Store, 3f);

        root.Update(new UpdateTick(0.16f, 0)); // Pending → Running
        root.Update(new UpdateTick(0.16f, 0)); // Running, 累加 Elapsed

        ref var delay = ref entity.GetComponent<DelayTaskComponent>();
        Assert.Equal(0.16f, delay.Elapsed, 4);
    }

    [Fact]
    public void RunningTask_TransitionsToDone_WhenElapsedExceedsDuration()
    {
        var (world, root) = Setup();
        var entity = CreateDelayTask(world.Store, 0.3f);

        root.Update(new UpdateTick(0.16f, 0)); // Pending → Running
        root.Update(new UpdateTick(0.16f, 0)); // Elapsed=0.16
        root.Update(new UpdateTick(0.16f, 0)); // Elapsed=0.32 → Done

        ref var state = ref entity.GetComponent<TaskStateComponent>();
        Assert.Equal(TaskState.Done, state.State);
    }

    [Fact]
    public void DoneTask_StaysDone_OnUpdate()
    {
        var (world, root) = Setup();
        var entity = CreateDelayTask(world.Store, 0.1f);

        root.Update(new UpdateTick(0.16f, 0)); // Pending → Running
        root.Update(new UpdateTick(0.16f, 0)); // Elapsed=0.16 → Done
        root.Update(new UpdateTick(0.16f, 0)); // 再做一次 Tick

        ref var state = ref entity.GetComponent<TaskStateComponent>();
        Assert.Equal(TaskState.Done, state.State); // 仍为 Done
    }

    [Fact]
    public void CancelledTask_StaysCancelled_OnUpdate()
    {
        var (world, root) = Setup();
        var entity = CreateDelayTask(world.Store, 3f);
        entity.GetComponent<TaskStateComponent>().State = TaskState.Cancelled;

        root.Update(new UpdateTick(0.16f, 0));

        ref var state = ref entity.GetComponent<TaskStateComponent>();
        Assert.Equal(TaskState.Cancelled, state.State); // 未被 System 改变
    }

    [Fact]
    public void DoneTask_IsNotAutoDeleted()
    {
        var (world, root) = Setup();
        var entity = CreateDelayTask(world.Store, 0.1f);

        root.Update(new UpdateTick(0.16f, 0)); // Pending → Running
        root.Update(new UpdateTick(0.16f, 0)); // Elapsed=0.16 → Done

        Assert.True(world.Store.GetEntityById(entity.Id).Id == entity.Id);
    }

    [Fact]
    public void DurationZero_CompletesInOneFrame()
    {
        var (world, root) = Setup();
        var entity = CreateDelayTask(world.Store, 0f);

        root.Update(new UpdateTick(0.16f, 0)); // Pending → Running（Elapsed=0, Elapsed>=0 true → Done）

        ref var state = ref entity.GetComponent<TaskStateComponent>();
        Assert.Equal(TaskState.Done, state.State);
    }
}
