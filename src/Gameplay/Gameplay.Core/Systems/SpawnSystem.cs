using System.Collections.Generic;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;

namespace Gameplay.Core;

/// <summary>按 PrefabId 实例化，一次性生成后移除 SpawnPoint。</summary>
public sealed class SpawnSystem : QuerySystem<SpawnPointComponent>
{
    private readonly List<(int PrefabId, int TeamId)> _pending = new();

    protected override void OnUpdate()
    {
        var store = Query.Store;
        _pending.Clear();
        Query.ForEachEntity((ref SpawnPointComponent spawnPoint, Entity entity) =>
        {
            _pending.Add((spawnPoint.PrefabId, spawnPoint.TeamId));
            CommandBuffer.RemoveComponent<SpawnPointComponent>(entity.Id);   // 一次性生成（经 CommandBuffer）
        });

        // 遍历结束后实例化（CreateEntity 不再影响已完成的 Query 遍历）
        for (int i = 0; i < _pending.Count; i++)
        {
            var (prefabId, teamId) = _pending[i];
            var prefab = PrefabRegistry.GetById(prefabId);
            if (prefab == null) continue;
            var spawned = prefab.Instantiate(store);
            if (teamId != 0)
                spawned.AddComponent(new TeamComponent { TeamId = teamId });
        }
    }
}
