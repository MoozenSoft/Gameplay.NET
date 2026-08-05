using Friflo.Engine.ECS;

namespace Gameplay.Tasks;

/// <summary>
/// 生成能力（Action 类）——克隆 <see cref="Prefab"/> 实体到指定位置，任务立即完成（Done）。<br/>
/// 对应 UE 的 SpawnActor。生成结果写回 <see cref="SpawnedEntity"/>，Task 存活期间可读
/// （完成回调 ITaskCompletionListener.OnAllTasksDone 内读取）。
/// </summary>
public struct SpawnRequestComponent : IComponent
{
    /// <summary>预制实体（克隆源）。</summary>
    public Entity Prefab;

    /// <summary>生成位置。</summary>
    public Position SpawnPosition;

    /// <summary>生成结果——SpawnSystem 生成后写回（无符号 Entity 表示生成失败）。</summary>
    public Entity SpawnedEntity;
}
