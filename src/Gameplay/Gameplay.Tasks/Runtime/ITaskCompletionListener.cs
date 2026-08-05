using Friflo.Engine.ECS;

namespace Gameplay.Tasks;

/// <summary>Task 完成监听器——当 Owner 的所有 Task 都完成（Done/Cancelled）时被 <see cref="TaskSchedulerSystem"/> 通知。</summary>
/// <remarks>
/// <para>由消费方实现（如 GAS 的 Ability 层：全部 Task 完成 → EndAbility；Quest：推进任务进度）。</para>
/// <para>回调内 Task 仍存活，可读取 Task 数据（如 MoveTo 的实际落点）。</para>
/// </remarks>
public interface ITaskCompletionListener
{
    /// <summary>Owner 的所有 Task 都已结束（Done/Cancelled）。</summary>
    void OnAllTasksDone(Entity owner);
}
