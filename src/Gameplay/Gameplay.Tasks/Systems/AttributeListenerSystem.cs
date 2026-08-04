using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Gameplay.Abilities;

namespace Gameplay.Tasks;

/// <summary>
/// 属性能力 Driver——每帧检查目标 Entity 的属性值，变化时 Task 完成（Done）。<br/>
/// 读取的是上一帧 Phase 4 Flush 后的已结算值，具有确定性。
/// </summary>
public class AttributeListenerSystem : QuerySystem<AttributeListener, TaskStateComponent>
{
    private readonly AttributeAggregatorManager mgr;

    public AttributeListenerSystem(AttributeAggregatorManager mgr)
    {
        this.mgr = mgr;
    }

    protected override void OnUpdate()
    {
        Query.ForEachEntity((ref AttributeListener listener, ref TaskStateComponent state, Entity entity) =>
        {
            if (state.State != ETaskState.Pending && state.State != ETaskState.Running)
                return;

            // Pending→Running 在 guard 之前，防止目标无效时任务卡在 Pending
            if (state.State == ETaskState.Pending)
            {
                state.State = ETaskState.Running;
                // 快照当前值——"变化"相对注册时，而非 0（否则初始值非 0 会误判为一次变化）
                var pendingTarget = listener.Target;
                if (!pendingTarget.IsNull)
                    listener.LastValue = mgr.GetCurrentValue(pendingTarget, listener.Attribute);
                return;
            }

            // 目标已销毁 → 无条件 Done
            var target = listener.Target;
            if (target.IsNull)
            {
                state.State = ETaskState.Done;
                return;
            }

            float current = mgr.GetCurrentValue(target, listener.Attribute);

            if (current != listener.LastValue)
            {
                listener.Count--;
                if (listener.Count <= 0)
                    state.State = ETaskState.Done;
                else
                    listener.LastValue = current;
            }
        });
    }
}
