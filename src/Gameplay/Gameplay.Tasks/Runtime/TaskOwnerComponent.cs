using Friflo.Engine.ECS;

namespace Gameplay.Tasks;

/// <summary>Task 的归属信息——谁创建了这个 Task（Ability / AI / Quest / 任意 Entity）。</summary>
/// <remarks>
/// <para>层次关系（<see cref="Entity.AddChild"/>/ChildEntities）用于 AllTasksDone 检测，Owner 引用用于完成通知。</para>
/// </remarks>
public struct TaskOwnerComponent : IComponent
{
    /// <summary>创建并拥有此 Task 的 Entity。Owner 的所有 Task 完成（Done/Cancelled）时收到通知。</summary>
    public Entity Owner;

    /// <summary>Task 句柄（预留，供后续 Task 管理使用）。</summary>
    public int TaskHandle;
}
