using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Gameplay.Abilities;

namespace Gameplay.Tasks;

/// <summary>
/// GE 能力 Driver——事件驱动：监听 EffectSystem 的施加/移除事件，<br/>
/// 匹配（Target + Query + Condition）的 Task 完成（Done）。<br/>
/// 与 GameplayEventSystem 同构（事件回调驱动，非每帧轮询）。<br/>
/// 注意：事件回调发生在 SystemRoot.Update 之外，此时 QuerySystem.Query 属性为 null
/// （Friflo OnUpdateGroup: SetQuery → OnUpdate → SetQuery(null)）——回调内用 store.Query 遍历。
/// </summary>
public class EffectListenerSystem : QuerySystem<EffectListener, TaskStateComponent>
{
    private readonly EffectSystem effectSystem;
    private readonly EntityStore store;
    private bool hooked;

    public EffectListenerSystem(EffectSystem effectSystem, EntityStore store)
    {
        this.effectSystem = effectSystem;
        this.store = store;
    }

    protected override void OnUpdate()
    {
        // 注册 GE 生命周期事件（仅一次）
        if (!hooked)
        {
            effectSystem.EffectApplied += OnEffectApplied;
            effectSystem.EffectRemoved += OnEffectRemoved;
            hooked = true;
        }

        // Pending → Running（事件驱动的任务仍走标准生命周期；事件只作用于 Running 之后）
        Query.ForEachEntity((ref EffectListener listener, ref TaskStateComponent state, Entity entity) =>
        {
            if (state.State == ETaskState.Pending)
                state.State = ETaskState.Running;
            else if (state.State == ETaskState.Running && listener.Target.IsNull)
                TaskCommands.Complete(entity); // 目标已销毁——事件永远等不到（级联删除不发 EffectRemoved），防御性完成
        });
    }

    private void OnEffectApplied(GameplayEffectSpec spec, Entity target)
        => CompleteMatching(spec, target, EEffectCondition.Applied);

    private void OnEffectRemoved(GameplayEffectSpec spec, Entity target, EEffectEndType reason)
        => CompleteMatching(spec, target, EEffectCondition.Removed);

    /// <summary>遍历 Running 状态的监听 Task，匹配（Target + Condition + Query）则完成。</summary>
    private void CompleteMatching(GameplayEffectSpec spec, Entity target, EEffectCondition condition)
    {
        // 事件回调在 Update 之外——Query 属性为 null，用 store.Query 遍历（store 内部缓存 ArchetypeQuery）
        var query = store.Query<EffectListener, TaskStateComponent>();
        query.ForEachEntity((ref EffectListener listener, ref TaskStateComponent state, Entity entity) =>
        {
            if (state.State != ETaskState.Running) return;
            if (listener.Condition != condition) return;
            if (listener.Target.Id != target.Id) return;
            if (!listener.Query.Matches(spec)) return;
            TaskCommands.Complete(entity);
        });
    }
}
