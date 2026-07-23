namespace Gameplay.Tests.Abilities;

using Gameplay.Abilities;
using Xunit;

public class EventSystemTests
{
    [Fact]
    public void Tick_DispatchesStaticHandler()
    {
        var bus = new GameplayEventBus();
        var sys = new EventSystem(bus);
        var handler = new TestHandler();
        sys.RegisterStatic(1, handler);

        bus.Enqueue(new GameplayEventRecord { EventId = 1, Magnitude = 42f });
        sys.Tick();

        Assert.Equal(42f, handler.LastMagnitude, 0.001f);
    }

    [Fact]
    public void Tick_DispatchesMultipleStaticHandlers()
    {
        var bus = new GameplayEventBus();
        var sys = new EventSystem(bus);
        var handler1 = new TestHandler();
        var handler2 = new TestHandler();
        sys.RegisterStatic(1, handler1);
        sys.RegisterStatic(1, handler2);

        bus.Enqueue(new GameplayEventRecord { EventId = 1, Magnitude = 10f });
        sys.Tick();

        Assert.Equal(10f, handler1.LastMagnitude, 0.001f);
        Assert.Equal(10f, handler2.LastMagnitude, 0.001f);
    }

    [Fact]
    public void Tick_OnlyDispatchesMatchingEventId()
    {
        var bus = new GameplayEventBus();
        var sys = new EventSystem(bus);
        var handler = new TestHandler();
        sys.RegisterStatic(2, handler);

        bus.Enqueue(new GameplayEventRecord { EventId = 1, Magnitude = 99f });
        sys.Tick();

        Assert.Equal(0f, handler.LastMagnitude, 0.001f);
    }

    [Fact]
    public void Tick_ResetsFrameAfterDispatch()
    {
        var bus = new GameplayEventBus();
        var sys = new EventSystem(bus);

        bus.Enqueue(new GameplayEventRecord { EventId = 1 });
        sys.Tick();

        // After Tick, the frame should be reset
        var frame = bus.Swap();
        Assert.Equal(0, frame.Records.Count);
    }

    private class TestHandler : IGameplayEventHandler
    {
        public float LastMagnitude;
        public void Handle(in GameplayEventRecord record) => LastMagnitude = record.Magnitude;
    }
}
