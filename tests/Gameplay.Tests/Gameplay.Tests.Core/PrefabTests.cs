using Friflo.Engine.ECS;
using Gameplay.Core;
using Xunit;

namespace Gameplay.Tests.Core;

public class PrefabTests
{
    [Fact]
    public void Instantiate_CreatesEntityWithComponents()
    {
        var prefab = Prefab.Define(b => b
            .With(new HealthComponent { Current = 100f, Max = 100f, IsAlive = true })
            .With(new TeamComponent { TeamId = 1 }));

        var store = new EntityStore();
        var entity = prefab.Instantiate(store);

        Assert.True(entity.HasComponent<HealthComponent>());
        Assert.True(entity.HasComponent<TeamComponent>());
        Assert.Equal(1, entity.GetComponent<TeamComponent>().TeamId);
    }

    [Fact]
    public void Registry_RegisterAndGetById()
    {
        var prefab = Prefab.Define(b => b.With<HealthComponent>());
        var id = PrefabRegistry.Register(prefab);

        var got = PrefabRegistry.GetById(id);
        Assert.Same(prefab, got);
    }
}
