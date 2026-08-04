using System.Collections.Generic;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;

namespace Gameplay.Tasks;

/// <summary>
/// Task 生命周期管理（替代旧 AbilityTaskSystem）。<br/>
/// 职责：
/// 1. 检测 Done/Cancelled 的 Task（状态为唯一事实来源，Driver System 负责推进状态）；<br/>
/// 2. 对每个结束的 Task，子 Entity 遍历检查 Owner 的所有 Task 是否全部结束（AllTasksDone）；<br/>
/// 3. 全部结束 → 通知 <see cref="ITaskCompletionListener"/>（当帧 Task 仍存活，可读数据）；<br/>
/// 4. 入队延迟销毁该 Task，帧末由 <see cref="ProcessPendingDeletions"/> 统一删除（Query 内不能 DeleteEntity）。<br/>
/// 不做的：Pending→Running 转移（各 Driver 负责，Pending 含能力专属初始化）。
/// </summary>
public class TaskSchedulerSystem : QuerySystem<TaskStateComponent, TaskOwnerComponent>
{
    private readonly List<Entity> pendingDeletions = new();
    private ITaskCompletionListener? completionListener;

    /// <summary>注册完成监听器（消费方在构造 Feature 时注入）。</summary>
    public void SetCompletionListener(ITaskCompletionListener listener)
        => completionListener = listener;

    protected override void OnUpdate()
    {
        Query.ForEachEntity((ref TaskStateComponent state, ref TaskOwnerComponent ctx, Entity entity) =>
        {
            if (state.State != ETaskState.Done && state.State != ETaskState.Cancelled)
                return;

            // 防止 Owner 已销毁后重复访问（Owner 子树删除时连带删除 Task）
            var owner = ctx.Owner;
            if (!owner.IsNull && AllTasksDone(owner))
                completionListener?.OnAllTasksDone(owner);

            pendingDeletions.Add(entity);
        });
    }

    /// <summary>处理延迟删除队列。需在 SystemRoot.Update 之后调用。</summary>
    public void ProcessPendingDeletions()
    {
        foreach (var entity in pendingDeletions)
        {
            if (entity.IsNull) continue;
            entity.DeleteEntity();
        }
        pendingDeletions.Clear();
    }

    /// <summary>遍历 Owner 的所有子 Entity，检查是否存在未结束的 Task。</summary>
    private static bool AllTasksDone(Entity owner)
    {
        var childEntities = owner.ChildEntities;
        foreach (var child in childEntities)
        {
            if (child.TryGetComponent<TaskStateComponent>(out var ts))
            {
                if (ts.State != ETaskState.Done && ts.State != ETaskState.Cancelled)
                    return false;
            }
        }
        return true;
    }
}
