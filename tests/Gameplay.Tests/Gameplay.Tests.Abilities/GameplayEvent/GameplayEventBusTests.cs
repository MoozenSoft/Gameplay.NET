namespace Gameplay.Tests.Abilities;

using Gameplay.Abilities;
using Xunit;

public class GameplayEventBusTests
{
    [Fact]
    public void Enqueue_GoesToPending()
    {
        var bus = new GameplayEventBus();
        bus.Enqueue(new GameplayEventRecord { EventId = 1, Magnitude = 5f });
        var frame = bus.Swap();
        Assert.Equal(1, frame.Records.Count);
        Assert.Equal(5f, frame.Records.GetRef(0).Magnitude, 0.001f);
    }

    [Fact]
    public void Swap_ReturnsPreviousPending()
    {
        var bus = new GameplayEventBus();
        bus.Enqueue(new GameplayEventRecord { EventId = 1 });
        var frame = bus.Swap();
        Assert.Equal(1, frame.Records.Count);

        // After swap, pending is empty, current has the event
        var frame2 = bus.Swap();
        Assert.Equal(0, frame2.Records.Count);
    }
}
