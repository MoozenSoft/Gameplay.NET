using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Gameplay.Tasks;

namespace Gameplay.Abilities;

/// <summary>等待指定 GameplayAttribute 的 CurrentValue 发生变化的 Task Component。</summary>
public struct WaitAttributeChangeComponent : IComponent
{
    /// <summary>监听的 AttributeId。</summary>
    public int AttributeId;
    /// <summary>注册时的快照值，用于比较变化。</summary>
    public float LastValue;
    /// <summary>等待次数（>0 表示等待多少次变化）。</summary>
    public int Count;
}

/// <summary>
/// WaitAttributeChange Task System —— 每帧检查 Owner Entity 的属性值，变化时 Task Done。
/// </summary>
public class WaitAttributeChangeTaskSystem : QuerySystem<WaitAttributeChangeComponent, TaskStateComponent, AbilityTaskContextComponent>
{
    private readonly AttributeSystem attrSys;

    public WaitAttributeChangeTaskSystem(AttributeSystem attrSys)
    {
        this.attrSys = attrSys;
    }

    protected override void OnUpdate()
    {
        Query.ForEachEntity((ref WaitAttributeChangeComponent wait, ref TaskStateComponent state,
            ref AbilityTaskContextComponent ctx, Entity entity) =>
        {
            if (state.State != TaskState.Pending && state.State != TaskState.Running)
                return;

            if (ctx.ActiveAbility.IsNull) return;
            if (!ctx.ActiveAbility.TryGetComponent<ActiveAbilityComponent>(out var activeComp))
                return;
            var owner = activeComp.Owner;
            if (owner.IsNull) return;

            float current = attrSys.GetCurrentValue(owner, wait.AttributeId);

            if (state.State == TaskState.Pending)
            {
                wait.LastValue = current;
                state.State = TaskState.Running;
                return;
            }

            if (current != wait.LastValue)
            {
                wait.Count--;
                if (wait.Count <= 0)
                    state.State = TaskState.Done;
                else
                    wait.LastValue = current;
            }
        });
    }
}
