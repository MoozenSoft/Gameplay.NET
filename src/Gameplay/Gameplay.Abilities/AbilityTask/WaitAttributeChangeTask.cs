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
            if (state.State != ETaskState.Pending && state.State != ETaskState.Running)
                return;

            // Pending→Running 在 guard 之前，防止 owner 无效时卡在 Pending
            if (state.State == ETaskState.Pending)
            {
                state.State = ETaskState.Running;
                return;
            }

            // ActiveAbility 已销毁 → 无条件 Done
            if (ctx.ActiveAbility.IsNull || !ctx.ActiveAbility.HasComponent<ActiveAbilityComponent>())
            {
                state.State = ETaskState.Done;
                return;
            }
            ref var activeComp = ref ctx.ActiveAbility.GetComponent<ActiveAbilityComponent>();
            var owner = activeComp.Owner;
            if (owner.IsNull) { state.State = ETaskState.Done; return; }

            float current = attrSys.GetCurrentValue(owner, wait.AttributeId);

            if (current != wait.LastValue)
            {
                wait.Count--;
                if (wait.Count <= 0)
                    state.State = ETaskState.Done;
                else
                    wait.LastValue = current;
            }
        });
    }
}
