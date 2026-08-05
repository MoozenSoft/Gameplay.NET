using System.Collections.Generic;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;

namespace Gameplay.Tasks;

/// <summary>
/// 生成能力 Driver（Action 类）——克隆预制实体到指定位置，任务立即完成（Done）。<br/>
/// 克隆/AddComponent 是结构变化，不能在 Query 循环内执行——先收集 Pending Task，帧末统一生成。
/// </summary>
public class SpawnSystem : QuerySystem<SpawnRequestComponent, TaskStateComponent>
{
    private readonly List<Entity> pendingSpawns = new();

    protected override void OnUpdate()
    {
        // 收集 Pending 的生成请求（结构变化不能 Query 循环内执行）
        Query.ForEachEntity((ref SpawnRequestComponent spawn, ref TaskStateComponent state, Entity entity) =>
        {
            if (state.State != ETaskState.Pending) return;
            state.State = ETaskState.Running;
            pendingSpawns.Add(entity);
        });

        // 帧末处理：克隆 + 设位置 + 完成（Query 循环外，结构变化安全）
        // try/finally——DoSpawn 异常也不残留列表（否则下帧对已完成 Task 重复生成）
        try
        {
            foreach (var task in pendingSpawns)
            {
                if (task.IsNull) continue;
                DoSpawn(task);
                TaskCommands.Complete(task);
            }
        }
        finally
        {
            pendingSpawns.Clear();
        }
    }

    private static void DoSpawn(Entity task)
    {
        ref var spawn = ref task.GetComponent<SpawnRequestComponent>();
        if (spawn.Prefab.IsNull)
            return; // 预制无效——无法生成（任务仍完成，避免挂起；SpawnedEntity 保持无符号）

        var clone = spawn.Prefab.CloneEntity();
        if (clone.HasComponent<Position>())
            clone.GetComponent<Position>() = spawn.SpawnPosition;
        else
            clone.AddComponent(spawn.SpawnPosition);

        spawn.SpawnedEntity = clone;
    }
}
