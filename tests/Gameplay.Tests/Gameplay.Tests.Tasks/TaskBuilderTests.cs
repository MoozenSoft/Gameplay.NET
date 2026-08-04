// tests/Gameplay.Tests/Gameplay.Tests.Abilities/AbilityTask/WaitDelayTaskTests.cs
namespace Gameplay.Tests.Tasks;

using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Gameplay.Abilities;
using Gameplay.Tasks;
using Xunit;

public class TaskBuilderTests
{
    private static Entity CreateWaitDelayTask(EntityStore store, float duration, Entity activeAbility)
        => TaskBuilder.Delay(store, duration, activeAbility);

    [Fact]
    public void WaitDelayTask_HasAllRequiredComponents()
    {
        var store = new EntityStore();
        var activeAbility = store.CreateEntity();
        var entity = CreateWaitDelayTask(store, 1f, activeAbility);

        Assert.True(entity.HasComponent<TaskStateComponent>());
        Assert.True(entity.HasComponent<TaskOwnerComponent>());
        Assert.True(entity.HasComponent<DelayComponent>());

        ref var ownerComp = ref entity.GetComponent<TaskOwnerComponent>();
        Assert.Equal(activeAbility.Id, ownerComp.Owner.Id);
        // Builder 把 Task 挂到 Owner（ActiveAbility）下，供 AllTasksDone 检测
        Assert.Equal(activeAbility.Id, entity.Parent.Id);
    }

    [Fact]
    public void WaitDelayTask_CompletesAfterDuration()
    {
        var store = new EntityStore();
        var activeAbility = store.CreateEntity();
        activeAbility.AddComponent(new ActiveAbilityComponent
        {
            Handle = new AbilityHandle(1),
            IsActive = true,
            Owner = store.CreateEntity(),
            State = EAbilityInstanceState.Active,
        });

        var taskEntity = CreateWaitDelayTask(store, 0.3f, activeAbility);

        var root = new SystemRoot(store)
        {
            new DelaySystem(),
        };

        root.Update(new UpdateTick(0.16f, 0)); // Pending → Running (Elapsed still 0)
        root.Update(new UpdateTick(0.16f, 0)); // Running → Elapsed=0.16
        root.Update(new UpdateTick(0.16f, 0)); // Running → Elapsed=0.32 >= 0.3 → Done

        ref var state = ref taskEntity.GetComponent<TaskStateComponent>();
        Assert.Equal(ETaskState.Done, state.State);
    }

    [Fact]
    public void WaitDelayTask_ZeroDuration_CompletesInOneFrame()
    {
        var store = new EntityStore();
        var activeAbility = store.CreateEntity();
        activeAbility.AddComponent(new ActiveAbilityComponent
        {
            Handle = new AbilityHandle(1),
            IsActive = true,
            Owner = store.CreateEntity(),
            State = EAbilityInstanceState.Active,
        });

        var taskEntity = CreateWaitDelayTask(store, 0f, activeAbility);

        var root = new SystemRoot(store)
        {
            new DelaySystem(),
        };

        root.Update(new UpdateTick(0.16f, 0));

        ref var state = ref taskEntity.GetComponent<TaskStateComponent>();
        Assert.Equal(ETaskState.Done, state.State);
    }
}
