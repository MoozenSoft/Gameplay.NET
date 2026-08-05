using Friflo.Engine.ECS;

namespace Gameplay.Tasks;

/// <summary>Task 状态命令——Driver System 的便捷门面。</summary>
/// <remarks>
/// <para>只负责状态转移（纯命令，无逻辑）；销毁由 <see cref="TaskSchedulerSystem"/> 在帧末统一执行。</para>
/// </remarks>
public static class TaskCommands
{
    /// <summary>标记 Task 完成（Done）。</summary>
    public static void Complete(Entity entity) => SetState(entity, ETaskState.Done);

    /// <summary>标记 Task 取消（Cancelled）。</summary>
    public static void Cancel(Entity entity) => SetState(entity, ETaskState.Cancelled);

    private static void SetState(Entity entity, ETaskState newState)
    {
        if (entity.IsNull || !entity.HasComponent<TaskStateComponent>())
            return;
        ref var state = ref entity.GetComponent<TaskStateComponent>();
        state.State = newState;
    }
}
