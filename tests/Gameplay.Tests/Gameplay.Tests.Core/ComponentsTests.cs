using Friflo.Engine.ECS;
using Gameplay.Core;
using Xunit;

namespace Gameplay.Tests.Core;

public class ComponentsTests
{
    [Fact]
    public void Components_CanAttachToEntity()
    {
        var store = new EntityStore();
        var entity = store.CreateEntity();

        entity.AddComponent(new TransformComponent { Position = new Vector3(1f, 2f, 3f) });
        entity.AddComponent(new VelocityComponent { Velocity = new Vector3(0f, 1f, 0f) });
        entity.AddComponent(new HealthComponent { Current = 100f, Max = 100f, IsAlive = true });
        entity.AddComponent(new TeamComponent { TeamId = 1 });
        entity.AddComponent(new OwnerComponent { PlayerId = -1 });
        entity.AddComponent(new TimerComponent { Remaining = 3f, Duration = 3f });
        entity.AddComponent(new LifetimeComponent { Remaining = 5f });
        entity.AddComponent(new SpawnPointComponent { PrefabId = 1, TeamId = 1 });
        entity.AddComponent(new PlayerStateComponent { PlayerId = 1 });

        ref var health = ref entity.GetComponent<HealthComponent>();
        Assert.Equal(100f, health.Current);
        Assert.True(health.IsAlive);
    }

    [Fact]
    public void HealthComponent_Modification_RequiresRef()
    {
        var store = new EntityStore();
        var entity = store.CreateEntity();
        entity.AddComponent(new HealthComponent { Current = 100f, Max = 100f, IsAlive = true });

        ref var health = ref entity.GetComponent<HealthComponent>();
        health.Current = 0f;   // ref 写回生效

        Assert.Equal(0f, entity.GetComponent<HealthComponent>().Current);
    }
}
