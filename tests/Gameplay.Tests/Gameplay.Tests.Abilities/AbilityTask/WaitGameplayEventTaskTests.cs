// tests/Gameplay.Tests/Gameplay.Tests.Abilities/AbilityTask/WaitGameplayEventTaskTests.cs
namespace Gameplay.Tests.Abilities;

using Friflo.Engine.ECS;
using Gameplay.Abilities;
using Gameplay.Tasks;
using Xunit;

public class WaitGameplayEventTaskTests
{
    [Fact]
    public void WaitGameplayEventComponent_StoresEventId()
    {
        var comp = new WaitGameplayEventComponent { EventId = 42 };
        Assert.Equal((ushort)42, comp.EventId);
    }

    [Fact]
    public void WaitGameplayEventComponent_DefaultEventId_IsZero()
    {
        var comp = new WaitGameplayEventComponent();
        Assert.Equal((ushort)0, comp.EventId);
    }

    [Fact]
    public void RegisterAndDispatch_MatchingEvent_SetsTaskStateToDone()
    {
        var store = new EntityStore();
        var bus = new GameplayEventBus();
        var eventDispatcher = new GameplayEventDispatcher(bus);
        ushort eventId = 5;

        // Create task entity with WaitGameplayEventComponent
        var taskEntity = store.CreateEntity();
        taskEntity.AddComponent(new TaskStateComponent { State = ETaskState.Pending });
        taskEntity.AddComponent(new AbilityTaskContextComponent());
        taskEntity.AddComponent(new WaitGameplayEventComponent { EventId = eventId });

        // Register as dynamic listener
        eventDispatcher.RegisterDynamic(eventId, taskEntity, 0);

        // Set up the dynamic dispatch handler via GameplayEventDispatcher
        eventDispatcher.OnDynamicInvoke = (in GameplayEventRecord record, int entityId, int handlerId) =>
        {
            if (entityId != taskEntity.Id) return;
            if (record.EventId != eventId) return;

            var entity = store.GetEntityById(entityId);
            if (entity.HasComponent<WaitGameplayEventComponent>())
            {
                ref var state = ref entity.GetComponent<TaskStateComponent>();
                state.State = ETaskState.Done;
            }
        };

        // Enqueue matching event
        bus.Enqueue(new GameplayEventRecord { EventId = eventId, Magnitude = 10f });
        eventDispatcher.Tick();

        ref var state = ref taskEntity.GetComponent<TaskStateComponent>();
        Assert.Equal(ETaskState.Done, state.State);
    }

    [Fact]
    public void RegisterAndDispatch_NonMatchingEvent_DoesNotChangeState()
    {
        var store = new EntityStore();
        var bus = new GameplayEventBus();
        var eventDispatcher = new GameplayEventDispatcher(bus);
        ushort eventId = 5;

        var taskEntity = store.CreateEntity();
        taskEntity.AddComponent(new TaskStateComponent { State = ETaskState.Pending });
        taskEntity.AddComponent(new AbilityTaskContextComponent());
        taskEntity.AddComponent(new WaitGameplayEventComponent { EventId = eventId });

        eventDispatcher.RegisterDynamic(eventId, taskEntity, 0);

        eventDispatcher.OnDynamicInvoke = (in GameplayEventRecord record, int entityId, int handlerId) =>
        {
            if (entityId != taskEntity.Id) return;
            if (record.EventId != eventId) return;

            var entity = store.GetEntityById(entityId);
            if (entity.HasComponent<WaitGameplayEventComponent>())
            {
                ref var state = ref entity.GetComponent<TaskStateComponent>();
                state.State = ETaskState.Done;
            }
        };

        // Enqueue event with DIFFERENT EventId
        bus.Enqueue(new GameplayEventRecord { EventId = 99, Magnitude = 10f });
        eventDispatcher.Tick();

        ref var state = ref taskEntity.GetComponent<TaskStateComponent>();
        Assert.Equal(ETaskState.Pending, state.State);
    }

    [Fact]
    public void UnregisterDynamic_StopsReceivingEvents()
    {
        var store = new EntityStore();
        var bus = new GameplayEventBus();
        var eventDispatcher = new GameplayEventDispatcher(bus);
        ushort eventId = 5;

        var taskEntity = store.CreateEntity();
        taskEntity.AddComponent(new TaskStateComponent { State = ETaskState.Pending });
        taskEntity.AddComponent(new AbilityTaskContextComponent());
        taskEntity.AddComponent(new WaitGameplayEventComponent { EventId = eventId });

        eventDispatcher.RegisterDynamic(eventId, taskEntity, 0);
        eventDispatcher.UnregisterDynamic(eventId, taskEntity, 0);

        bool wasInvoked = false;
        eventDispatcher.OnDynamicInvoke = (in GameplayEventRecord _, int entityId, int _2) =>
        {
            if (entityId == taskEntity.Id)
                wasInvoked = true;
        };

        bus.Enqueue(new GameplayEventRecord { EventId = eventId });
        eventDispatcher.Tick();

        Assert.False(wasInvoked);
    }
}
