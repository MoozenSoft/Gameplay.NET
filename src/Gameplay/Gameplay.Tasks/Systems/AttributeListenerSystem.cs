using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Gameplay.Abilities;

namespace Gameplay.Tasks;

/// <summary>
/// 属性能力 Driver——每帧检查目标 Entity 的属性值是否满足监听条件，满足时 Task 完成（Done）。<br/>
/// <see cref="EAttributeCondition.Changed"/> 为边沿触发（相对注册时快照）；
/// 阈值模式（Above/Below/RatioAbove/RatioBelow）为电平触发（每帧比较当前值，注册时已满足则下一帧完成）。<br/>
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
                // Changed 模式：快照当前值——"变化"相对注册时，而非 0（否则初始值非 0 会误判为一次变化）
                if (listener.Condition == EAttributeCondition.Changed)
                {
                    var pendingTarget = listener.Target;
                    if (!pendingTarget.IsNull)
                        listener.LastValue = mgr.GetCurrentValue(pendingTarget, listener.Attribute);
                }
                return;
            }

            // 目标已销毁 → 无条件 Done
            var target = listener.Target;
            if (target.IsNull)
            {
                TaskCommands.Complete(entity);
                return;
            }

            switch (listener.Condition)
            {
                case EAttributeCondition.Changed:
                    float current = mgr.GetCurrentValue(target, listener.Attribute);
                    if (current != listener.LastValue)
                    {
                        listener.Count--;
                        if (listener.Count <= 0)
                            TaskCommands.Complete(entity);
                        else
                            listener.LastValue = current;
                    }
                    break;

                case EAttributeCondition.Above:
                    if (mgr.GetCurrentValue(target, listener.Attribute) > listener.Threshold)
                        TaskCommands.Complete(entity);
                    break;

                case EAttributeCondition.Below:
                    if (mgr.GetCurrentValue(target, listener.Attribute) < listener.Threshold)
                        TaskCommands.Complete(entity);
                    break;

                case EAttributeCondition.RatioAbove:
                case EAttributeCondition.RatioBelow:
                {
                    float baseValue = mgr.GetBaseValue(target, listener.Attribute);
                    // Base=0 时比值无意义——跳过判定，避免 0/0=NaN、x/0=Infinity 造成
                    // 永久挂起（NaN 比较恒 false）或瞬时误完成（Infinity 比较恒 true）
                    if (baseValue == 0f)
                        break;
                    float ratio = mgr.GetCurrentValue(target, listener.Attribute) / baseValue;
                    if (listener.Condition == EAttributeCondition.RatioAbove
                        ? ratio > listener.Threshold
                        : ratio < listener.Threshold)
                        TaskCommands.Complete(entity);
                    break;
                }

                // 未知条件：不完成（防未来枚举扩展静默改变行为）
                default:
                    break;
            }
        });
    }
}
