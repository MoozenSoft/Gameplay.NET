// src/Gameplay/Gameplay.Abilities/AbilityTask/AbilityTaskSystem.cs
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Gameplay.Tasks;

namespace Gameplay.Abilities;

/// <summary>
/// 监听 ActiveAbility 下所有 Task 的状态变化。
/// 当全部 Task 变为 Done 或 Cancelled 时，触发 CancelAbility 结束 Ability。
/// </summary>
public class AbilityTaskSystem : QuerySystem<TaskStateComponent, AbilityTaskContextComponent>
{
    private readonly AbilityActivationSystem activationSystem;

    public AbilityTaskSystem(AbilityActivationSystem sys)
    {
        activationSystem = sys;
    }

    protected override void OnUpdate()
    {
        Query.ForEachEntity((ref TaskStateComponent state, ref AbilityTaskContextComponent ctx, Entity entity) =>
        {
            if (state.State == TaskState.Done || state.State == TaskState.Cancelled)
            {
                // 防止 CancelAbility 已删除 ActiveAbility 后重复访问
                var activeAbility = ctx.ActiveAbility;
                if (activeAbility.IsNull)
                    return;

                // 检查 ActiveAbility 下所有 Task 是否都 Done/Cancelled
                if (AllTasksDone(activeAbility))
                    activationSystem.CancelAbility(activeAbility);
            }
        });
    }

    /// <summary>遍历 ActiveAbility Entity 的所有子 Entity，检查是否存在未完成的 Task。</summary>
    private static bool AllTasksDone(Entity activeAbility)
    {
        var childEntities = activeAbility.ChildEntities;
        foreach (var child in childEntities)
        {
            if (child.TryGetComponent<TaskStateComponent>(out var ts))
            {
                if (ts.State != TaskState.Done && ts.State != TaskState.Cancelled)
                    return false;
            }
        }
        return true;
    }
}
