using Gameplay.Core;
using Xunit;

namespace Gameplay.Tests.Core;

public class SpawnSystemTests
{
    [Fact]
    public void Update_InstantiatesPrefab_AtSpawnPointPosition_AndRemovesSpawnPoint()
    {
        var prefab = Prefab.Define(b =>
        {
            b.With(new HealthComponent { Current = 50f, Max = 50f, IsAlive = true });
            b.With(new TransformComponent { Position = new Vector3(1f, 0f, 0f), Scale = 1f });
        });
        var prefabId = PrefabRegistry.Register(prefab);

        var world = new World(ENetMode.Standalone);
        world.AddSystem(new SpawnSystem(), ESimulationStage.Simulation);
        var spawnPoint = world.Store.CreateEntity();
        spawnPoint.AddComponent(new SpawnPointComponent { PrefabId = prefabId, TeamId = 1 });
        spawnPoint.AddComponent(new TransformComponent { Position = new Vector3(5f, 0f, 0f), Scale = 1f });

        world.Update(0.16f);

        Assert.False(spawnPoint.HasComponent<SpawnPointComponent>());   // 一次性生成后移除

        int count = 0;
        foreach (var e in world.Store.Query<HealthComponent>().Entities)
        {
            count++;
            Assert.Equal(50f, e.GetComponent<HealthComponent>().Current);
            // 生成实体位置被 SpawnPoint 覆盖（Prefab 默认位置 1,0,0 → SpawnPoint 位置 5,0,0）
            Assert.Equal(new Vector3(5f, 0f, 0f), e.GetComponent<TransformComponent>().Position);
        }
        Assert.Equal(1, count);   // 恰好生成一个实例
    }
}
