using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Gameplay.Tags;

namespace Gameplay.Tasks;

/// <summary>
/// Tag 能力 Driver——每帧检查目标 Entity 的 Tag 状态变化（Added / Removed）。<br/>
/// 同一 Query 内按 <see cref="TagCondition"/> 分支——同一能力内的条件分支，不是跨能力 switch。
/// </summary>
public class TagListenerSystem : QuerySystem<TagListenerComponent, TaskStateComponent>
{
    protected override void OnUpdate()
    {
        Query.ForEachEntity((ref TagListenerComponent listener, ref TaskStateComponent state, Entity entity) =>
        {
            if (state.State != ETaskState.Pending && state.State != ETaskState.Running) return;

            // Pending→Running 在 guard 之前，防止目标无效时任务卡在 Pending
            if (state.State == ETaskState.Pending)
            {
                // Removed：如果条件本来就不满足，立即 Done
                if (listener.Condition == TagCondition.Removed)
                {
                    var pendingTarget = listener.Target;
                    if (IsConditionMet(pendingTarget, listener))
                    {
                        listener.WasPresent = true;
                        state.State = ETaskState.Running;
                    }
                    else
                    {
                        TaskCommands.Complete(entity); // 条件已满足（Tag 不存在）→ 已完成
                    }
                    return;
                }
                state.State = ETaskState.Running;
                return;
            }

            var target = listener.Target;
            if (target.IsNull || !target.TryGetComponent<GameplayTagsComponent>(out var tags))
            {
                TaskCommands.Complete(entity);
                return;
            }

            bool met = listener.RequiredTags != null
                ? tags.HasAll(listener.RequiredTags)
                : tags.HasTag(listener.Tag);
            if (listener.Condition == TagCondition.Added)
            {
                // Added：检查条件是否已出现
                if (met)
                    TaskCommands.Complete(entity);
            }
            else
            {
                // Removed：检查条件是否已被破坏
                if (listener.WasPresent && !met)
                    TaskCommands.Complete(entity);
            }
        });
    }

    /// <summary>
    /// 判定条件是否满足：单 Tag 模式查 HasTag；Query 模式（RequiredTags 非空）查 HasAll。
    /// </summary>
    private static bool IsConditionMet(Entity target, in TagListenerComponent listener)
    {
        if (target.IsNull || !target.TryGetComponent<GameplayTagsComponent>(out var tags))
            return false;
        return listener.RequiredTags != null
            ? tags.HasAll(listener.RequiredTags)
            : tags.HasTag(listener.Tag);
    }
}
