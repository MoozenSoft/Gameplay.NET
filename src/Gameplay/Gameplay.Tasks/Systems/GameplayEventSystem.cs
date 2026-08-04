using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Gameplay.Abilities;

namespace Gameplay.Tasks;

/// <summary>
/// 事件能力 Driver——管理 GameplayEventListener 的注册与事件分发。<br/>
/// 1. 为 Pending Task 注册 GameplayEventDispatcher 动态 Listener。<br/>
/// 2. 通过 OnDynamicInvoke 回调，在匹配事件到达时将 Task 设为 Done。
/// </summary>
public class GameplayEventSystem : QuerySystem<GameplayEventListener, TaskStateComponent>
{
    private readonly GameplayEventDispatcher eventDispatcher;
    private readonly EntityStore store;
    private bool callbackRegistered;

    public GameplayEventSystem(GameplayEventDispatcher eventDispatcher, EntityStore store)
    {
        this.eventDispatcher = eventDispatcher;
        this.store = store;
    }

    protected override void OnUpdate()
    {
        // 注册动态分发回调（仅一次）
        if (!callbackRegistered)
        {
            eventDispatcher.OnDynamicInvoke += HandleDynamicInvoke;
            callbackRegistered = true;
        }

        // 为 Pending Task 注册 GameplayEventDispatcher 动态 Listener
        Query.ForEachEntity((ref GameplayEventListener listener, ref TaskStateComponent state, Entity entity) =>
        {
            if (state.State == ETaskState.Pending)
            {
                eventDispatcher.RegisterDynamic(listener.EventId, entity, 0);
                state.State = ETaskState.Running;
            }
            else if (state.State == ETaskState.Done || state.State == ETaskState.Cancelled)
            {
                eventDispatcher.UnregisterDynamic(listener.EventId, entity, 0);
            }
        });
    }

    /// <summary>
    /// GameplayEventDispatcher 动态分发回调。
    /// 当事件 ID 匹配 GameplayEventListener.EventId 时，将 Task 设为 Done。
    /// </summary>
    private void HandleDynamicInvoke(in GameplayEventRecord record, int entityId, int handlerId)
    {
        var entity = store.GetEntityById(entityId);
        if (entity.IsNull)
            return;

        if (entity.TryGetComponent<GameplayEventListener>(out var listener))
        {
            if (listener.EventId == record.EventId)
            {
                ref var state = ref entity.GetComponent<TaskStateComponent>();
                state.State = ETaskState.Done;
            }
        }
    }
}
