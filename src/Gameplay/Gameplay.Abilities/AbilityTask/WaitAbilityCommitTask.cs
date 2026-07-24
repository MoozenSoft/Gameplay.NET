using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Gameplay.Tasks;

namespace Gameplay.Abilities;

/// <summary>等待 ActiveAbility 的 Commit 阶段完成的 Task Component。</summary>
public struct WaitAbilityCommitComponent : IComponent
{
    /// <summary>归属的 ActiveAbility Handle。</summary>
    public int AbilityHandle;
}

/// <summary>
/// WaitAbilityCommit Task System —— 当 ActiveAbility 的 State 变为 Active（Commit 已完成）时 Task Done。
/// </summary>
public class WaitAbilityCommitTaskSystem : QuerySystem<WaitAbilityCommitComponent, TaskStateComponent, AbilityTaskContextComponent>
{
    protected override void OnUpdate()
    {
        Query.ForEachEntity((ref WaitAbilityCommitComponent wait, ref TaskStateComponent state,
            ref AbilityTaskContextComponent ctx, Entity entity) =>
        {
            if (state.State != TaskState.Pending && state.State != TaskState.Running)
                return;

            if (state.State == TaskState.Pending)
            {
                state.State = TaskState.Running;
                return;
            }

            // 检查 ActiveAbility 的 State：Active = Commit 完成
            if (!ctx.ActiveAbility.IsNull &&
                ctx.ActiveAbility.TryGetComponent<ActiveAbilityComponent>(out var activeComp))
            {
                if (activeComp.State == EAbilityInstanceState.Active)
                    state.State = TaskState.Done;
            }
        });
    }
}
