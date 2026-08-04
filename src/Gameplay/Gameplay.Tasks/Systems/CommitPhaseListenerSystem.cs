using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Gameplay.Abilities;

namespace Gameplay.Tasks;

/// <summary>
/// Commit 阶段能力 Driver——当目标 ActiveAbility 的 State 变为 Active（Commit 已完成）时 Task 完成（Done）。<br/>
/// 注：引用 Gameplay.Abilities 域类型（ActiveAbilityComponent / EAbilityInstanceState），技术债见计划。
/// </summary>
public class CommitPhaseListenerSystem : QuerySystem<CommitPhaseListener, TaskStateComponent>
{
    protected override void OnUpdate()
    {
        Query.ForEachEntity((ref CommitPhaseListener listener, ref TaskStateComponent state, Entity entity) =>
        {
            if (state.State != ETaskState.Pending && state.State != ETaskState.Running)
                return;

            if (state.State == ETaskState.Pending)
            {
                state.State = ETaskState.Running;
                return;
            }

            // 目标 ActiveAbility 已销毁 → 无条件 Done
            var target = listener.Target;
            if (target.IsNull || !target.HasComponent<ActiveAbilityComponent>())
            {
                state.State = ETaskState.Done;
            }
            else
            {
                ref var activeComp = ref target.GetComponent<ActiveAbilityComponent>();
                if (activeComp.State == EAbilityInstanceState.Active)
                    state.State = ETaskState.Done;
            }
        });
    }
}
