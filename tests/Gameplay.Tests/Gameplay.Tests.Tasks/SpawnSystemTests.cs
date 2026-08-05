// tests/Gameplay.Tests/Gameplay.Tests.Tasks/SpawnSystemTests.cs
namespace Gameplay.Tests.Tasks;

using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Gameplay;
using Gameplay.Tasks;
using Xunit;

/// <summary>对应 src/Gameplay/Gameplay.Tasks/Systems/SpawnSystem.cs——克隆预制 → 设位置 → 立即完成。</summary>
public class SpawnSystemTests
{
    private static (Entity Task, SystemRoot Root) Setup(EntityStore store, Entity prefab, Position spawnPosition)
    {
        var task = TaskBuilder.SpawnActor(store, prefab, spawnPosition, owner: store.CreateEntity());
        var root = new SystemRoot(store) { new SpawnSystem() };
        return (task, root);
    }

    private static ETaskState GetState(Entity task)
        => task.GetComponent<TaskStateComponent>().State;

    [Fact]
    public void SpawnActor_ClonesPrefab_WithComponents()
    {
        var store = new EntityStore();
        var prefab = store.CreateEntity();
        prefab.AddComponent(new HealthComponent { Value = 100f });

        var (task, root) = Setup(store, prefab, new Position(1f, 2f, 3f));

        root.Update(new UpdateTick(0.16f, 0)); // Pending → 帧末生成 + 完成

        Assert.Equal(ETaskState.Done, GetState(task));

        var spawned = task.GetComponent<SpawnRequestComponent>().SpawnedEntity;
        Assert.False(spawned.IsNull);
        Assert.NotEqual(prefab.Id, spawned.Id); // 是新的实体
        Assert.Equal(100f, spawned.GetComponent<HealthComponent>().Value); // 组件被克隆
        Assert.Equal(new Position(1f, 2f, 3f), spawned.GetComponent<Position>()); // 位置被设置
    }

    [Fact]
    public void SpawnActor_PrefabWithoutPosition_GetsPositionAdded()
    {
        var store = new EntityStore();
        var prefab = store.CreateEntity(); // 无 Position 组件

        var (task, root) = Setup(store, prefab, new Position(5f, 0f, 0f));

        root.Update(new UpdateTick(0.16f, 0));

        var spawned = task.GetComponent<SpawnRequestComponent>().SpawnedEntity;
        Assert.False(spawned.IsNull);
        Assert.True(spawned.HasComponent<Position>());
        Assert.Equal(new Position(5f, 0f, 0f), spawned.GetComponent<Position>());
    }

    [Fact]
    public void SpawnActor_InvalidPrefab_CompletesWithoutSpawning()
    {
        var store = new EntityStore();

        var (task, root) = Setup(store, default, new Position(0f, 0f, 0f)); // Prefab 无符号

        root.Update(new UpdateTick(0.16f, 0));

        // 任务仍完成（不挂起）；SpawnedEntity 保持无符号
        Assert.Equal(ETaskState.Done, GetState(task));
        Assert.True(task.GetComponent<SpawnRequestComponent>().SpawnedEntity.IsNull);
    }

    [Fact]
    public void SpawnActor_AlreadyRunning_DoesNotRespawn()
    {
        var store = new EntityStore();
        var prefab = store.CreateEntity();
        prefab.AddComponent(new HealthComponent { Value = 100f });

        var (task, root) = Setup(store, prefab, new Position(1f, 1f, 1f));

        root.Update(new UpdateTick(0.16f, 0)); // 生成 + Done
        var firstSpawn = task.GetComponent<SpawnRequestComponent>().SpawnedEntity;

        root.Update(new UpdateTick(0.16f, 0)); // 已 Done，不重复生成

        Assert.Equal(firstSpawn.Id, task.GetComponent<SpawnRequestComponent>().SpawnedEntity.Id);
    }
}
