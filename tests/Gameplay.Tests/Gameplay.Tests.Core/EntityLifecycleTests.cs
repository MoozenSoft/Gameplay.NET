using Friflo.Engine.ECS;
using Gameplay.Core;
using Xunit;

namespace Gameplay.Tests.Core;

public class EntityLifecycleTests
{
    [Fact]
    public void Subscribe_ReceivesEntityCreatedEvent()
    {
        var world = new World(ENetMode.Standalone);
        EntityLifecycleEvent received = default;
        EntityLifecycle.Subscribe(world, (in EntityLifecycleEvent evt) => received = evt);

        var entity = world.Store.CreateEntity();

        Assert.Equal(EEntityLifecycleType.EntityCreated, received.Type);
        Assert.Equal(entity.Id, received.Entity.Id);
    }

    [Fact]
    public void Subscribe_ReceivesEntityDeletedEvent()
    {
        var world = new World(ENetMode.Standalone);
        EntityLifecycleEvent received = default;
        EntityLifecycle.Subscribe(world, (in EntityLifecycleEvent evt) => received = evt);

        var entity = world.Store.CreateEntity();
        entity.DeleteEntity();

        Assert.Equal(EEntityLifecycleType.EntityDeleted, received.Type);
        Assert.Equal(entity.Id, received.Entity.Id);
    }

    [Fact]
    public void Unsubscribe_StopsReceiving()
    {
        var world = new World(ENetMode.Standalone);
        int count = 0;
        EntityLifecycleHandler handler = (in EntityLifecycleEvent evt) => count++;
        EntityLifecycle.Subscribe(world, handler);
        EntityLifecycle.Unsubscribe(world, handler);

        world.Store.CreateEntity();

        Assert.Equal(0, count);
    }
}
