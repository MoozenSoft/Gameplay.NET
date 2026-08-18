using Friflo.Engine.ECS;
using Gameplay.Core;
using Xunit;

namespace Gameplay.Tests.Core;

public class EventBusTests
{
    private sealed class Counter : IEventHandler<EntityDeathEvent>
    {
        public int Count;
        public void Handle(in EntityDeathEvent evt) => Count++;
    }

    [Fact]
    public void Tick_DeliversEnqueuedEvent()
    {
        var bus = new EventBus();
        var handler = new Counter();
        bus.Subscribe<EntityDeathEvent>(handler);
        var store = new EntityStore();
        var entity = store.CreateEntity();

        bus.Enqueue(new EntityDeathEvent { Entity = entity });
        bus.Tick();

        Assert.Equal(1, handler.Count);
    }

    [Fact]
    public void Tick_DeliversToAllSubscribers()
    {
        var bus = new EventBus();
        var h1 = new Counter();
        var h2 = new Counter();
        bus.Subscribe<EntityDeathEvent>(h1);
        bus.Subscribe<EntityDeathEvent>(h2);
        var entity = new EntityStore().CreateEntity();

        bus.Enqueue(new EntityDeathEvent { Entity = entity });
        bus.Tick();

        Assert.Equal(1, h1.Count);
        Assert.Equal(1, h2.Count);
    }

    [Fact]
    public void Tick_NoEnqueue_NoDelivery()
    {
        var bus = new EventBus();
        var handler = new Counter();
        bus.Subscribe<EntityDeathEvent>(handler);
        bus.Tick();
        Assert.Equal(0, handler.Count);
    }

    [Fact]
    public void Unsubscribe_StopsDelivery()
    {
        var bus = new EventBus();
        var handler = new Counter();
        bus.Subscribe<EntityDeathEvent>(handler);
        bus.Unsubscribe<EntityDeathEvent>(handler);
        var entity = new EntityStore().CreateEntity();
        bus.Enqueue(new EntityDeathEvent { Entity = entity });
        bus.Tick();
        Assert.Equal(0, handler.Count);
    }
}
