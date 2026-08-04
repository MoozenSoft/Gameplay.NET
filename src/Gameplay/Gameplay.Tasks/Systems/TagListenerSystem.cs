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
                // Removed：如果 tag 本来就不在，立即 Done
                if (listener.Condition == TagCondition.Removed)
                {
                    var pendingTarget = listener.Target;
                    if (!pendingTarget.IsNull && pendingTarget.TryGetComponent<GameplayTagsComponent>(out var t) && t.HasTag(listener.Tag))
                    {
                        listener.WasPresent = true;
                        state.State = ETaskState.Running;
                    }
                    else
                    {
                        TaskCommands.Complete(entity); // Tag 不存在 → 已完成
                    }
                    return;
                }
                state.State = ETaskState.Running;
                return;
            }

            var target = listener.Target;
            if (target.IsNull || !target.HasComponent<GameplayTagsComponent>())
            {
                TaskCommands.Complete(entity);
                return;
            }

            ref var tags = ref target.GetComponent<GameplayTagsComponent>();
            if (listener.Condition == TagCondition.Added)
            {
                // Added：检查 tag 是否已出现
                if (tags.HasTag(listener.Tag))
                    TaskCommands.Complete(entity);
            }
            else
            {
                // Removed：检查 tag 是否已被移除
                bool hasNow = tags.HasTag(listener.Tag);
                if (listener.WasPresent && !hasNow)
                    TaskCommands.Complete(entity);
            }
        });
    }
}
