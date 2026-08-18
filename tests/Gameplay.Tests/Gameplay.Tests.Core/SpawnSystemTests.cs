using Gameplay.Core;
using Xunit;

namespace Gameplay.Tests.Core;

public class SpawnSystemTests
{
    [Fact]
    public void Update_InstantiatesPrefab_AndRemovesSpawnPoint()
    {
        var prefab = Prefab.Define(b => b.With(new HealthComponent { Current = 50f, Max = 50f, IsAlive = true }));
        var prefabId = PrefabRegistry.Register(prefab);

        var world = new World(ENetMode.Standalone);
        world.AddSystem(new SpawnSystem(), ESimulationStage.Simulation);
        var spawnPoint = world.Store.CreateEntity();
        spawnPoint.AddComponent(new SpawnPointComponent { PrefabId = prefabId, TeamId = 1 });

        world.Update(0.16f);

        Assert.False(spawnPoint.HasComponent<SpawnPointComponent>());   // 一次性生成后移除

        int count = 0;
        foreach (var e in world.Store.Query<HealthComponent>().Entities)
        {
            count++;
            Assert.Equal(50f, e.GetComponent<HealthComponent>().Current);
        }
        Assert.Equal(1, count);   // 恰好生成一个实例
    }
}
