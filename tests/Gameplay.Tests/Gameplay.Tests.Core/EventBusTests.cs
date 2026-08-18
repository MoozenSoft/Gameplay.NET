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

    private struct OtherEvent : IEvent
    {
        public int Value;
    }

    private sealed class OtherCounter : IEventHandler<OtherEvent>
    {
        public int Count;
        public void Handle(in OtherEvent evt) => Count++;
    }

    private sealed class EnqueueingHandler : IEventHandler<EntityDeathEvent>
    {
        private readonly EventBus _bus;

        public EnqueueingHandler(EventBus bus) => _bus = bus;

        public void Handle(in EntityDeathEvent evt) => _bus.Enqueue(new OtherEvent { Value = 1 });
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

    [Fact]
    public void Tick_EnqueueNewTypeDuringDispatch_NoThrowAndDeliversNextFrame()
    {
        var bus = new EventBus();
        var other = new OtherCounter();
        bus.Subscribe<EntityDeathEvent>(new EnqueueingHandler(bus));
        var entity = new EntityStore().CreateEntity();

        // 分发中 handler 首次 Enqueue<OtherEvent>（此前从未入队/订阅过，_queues 无该类型队列），不应抛异常
        bus.Enqueue(new EntityDeathEvent { Entity = entity });
        bus.Tick();

        Assert.Equal(0, other.Count);   // OtherEvent 落入下一帧
        bus.Subscribe<OtherEvent>(other);
        bus.Tick();                     // 下一帧正常分发
        Assert.Equal(1, other.Count);
    }
}
