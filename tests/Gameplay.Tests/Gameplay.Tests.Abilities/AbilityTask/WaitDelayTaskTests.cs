// tests/Gameplay.Tests/Gameplay.Tests.Abilities/AbilityTask/WaitDelayTaskTests.cs
namespace Gameplay.Tests.Abilities;

using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Gameplay.Abilities;
using Gameplay.Tasks;
using Xunit;

public class WaitDelayTaskTests
{
    private static Entity CreateWaitDelayTask(EntityStore store, float duration, Entity activeAbility)
    {
        var entity = store.CreateEntity();
        entity.AddComponent(new TaskStateComponent { State = TaskState.Pending });
        entity.AddComponent(new TaskOwnerComponent { Owner = default });
        entity.AddComponent(new DelayTaskComponent { Duration = duration, Elapsed = 0f });
        entity.AddComponent(new AbilityTaskContextComponent { ActiveAbility = activeAbility });
        return entity;
    }

    [Fact]
    public void WaitDelayTask_HasAllRequiredComponents()
    {
        var store = new EntityStore();
        var activeAbility = store.CreateEntity();
        var entity = CreateWaitDelayTask(store, 1f, activeAbility);

        Assert.True(entity.HasComponent<TaskStateComponent>());
        Assert.True(entity.HasComponent<TaskOwnerComponent>());
        Assert.True(entity.HasComponent<DelayTaskComponent>());
        Assert.True(entity.HasComponent<AbilityTaskContextComponent>());

        ref var ctx = ref entity.GetComponent<AbilityTaskContextComponent>();
        Assert.Equal(activeAbility.Id, ctx.ActiveAbility.Id);
    }

    [Fact]
    public void WaitDelayTask_CompletesAfterDuration()
    {
        var store = new EntityStore();
        var activeAbility = store.CreateEntity();
        activeAbility.AddComponent(new ActiveAbilityComponent
        {
            Handle = 1,
            IsActive = true,
            Owner = store.CreateEntity(),
            State = EAbilityInstanceState.Active,
        });

        var taskEntity = CreateWaitDelayTask(store, 0.3f, activeAbility);

        var root = new SystemRoot(store)
        {
            new DelayTaskSystem(),
        };

        root.Update(new UpdateTick(0.16f, 0)); // Pending → Running (Elapsed still 0)
        root.Update(new UpdateTick(0.16f, 0)); // Running → Elapsed=0.16
        root.Update(new UpdateTick(0.16f, 0)); // Running → Elapsed=0.32 >= 0.3 → Done

        ref var state = ref taskEntity.GetComponent<TaskStateComponent>();
        Assert.Equal(TaskState.Done, state.State);
    }

    [Fact]
    public void WaitDelayTask_ZeroDuration_CompletesInOneFrame()
    {
        var store = new EntityStore();
        var activeAbility = store.CreateEntity();
        activeAbility.AddComponent(new ActiveAbilityComponent
        {
            Handle = 1,
            IsActive = true,
            Owner = store.CreateEntity(),
            State = EAbilityInstanceState.Active,
        });

        var taskEntity = CreateWaitDelayTask(store, 0f, activeAbility);

        var root = new SystemRoot(store)
        {
            new DelayTaskSystem(),
        };

        root.Update(new UpdateTick(0.16f, 0));

        ref var state = ref taskEntity.GetComponent<TaskStateComponent>();
        Assert.Equal(TaskState.Done, state.State);
    }
}
