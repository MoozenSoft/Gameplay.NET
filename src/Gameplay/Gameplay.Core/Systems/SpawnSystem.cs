using System.Collections.Generic;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;

namespace Gameplay.Core;

/// <summary>按 PrefabId 实例化到 SpawnPoint 位置，一次性生成后移除 SpawnPoint。
/// SpawnPoint 实体需挂 TransformComponent 提供生成位置；生成实体若有 TransformComponent 则写回该位置。</summary>
public sealed class SpawnSystem : QuerySystem<SpawnPointComponent, TransformComponent>
{
    private readonly List<(int PrefabId, int TeamId, Vector3 Position)> pending = new();

    protected override void OnUpdate()
    {
        var store = Query.Store;
        pending.Clear();
        Query.ForEachEntity((ref SpawnPointComponent spawnPoint, ref TransformComponent transform, Entity entity) =>
        {
            pending.Add((spawnPoint.PrefabId, spawnPoint.TeamId, transform.Position));
            CommandBuffer.RemoveComponent<SpawnPointComponent>(entity.Id);   // 一次性生成（经 CommandBuffer）
        });

        // 遍历结束后实例化（CreateEntity 不再影响已完成的 Query 遍历）
        for (int i = 0; i < pending.Count; i++)
        {
            var (prefabId, teamId, position) = pending[i];
            var prefab = PrefabRegistry.GetById(prefabId);
            if (prefab == null) continue;
            var spawned = prefab.Instantiate(store);
            if (teamId != 0)
                spawned.AddComponent(new TeamComponent { TeamId = teamId });
            if (spawned.HasComponent<TransformComponent>())
            {
                ref var t = ref spawned.GetComponent<TransformComponent>();
                t.Position = position;   // 覆盖为 SpawnPoint 位置
            }
        }
    }
}
