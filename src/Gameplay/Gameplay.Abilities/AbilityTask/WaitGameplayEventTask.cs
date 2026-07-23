// src/Gameplay/Gameplay.Abilities/AbilityTask/WaitGameplayEventTask.cs
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Gameplay.Tasks;

namespace Gameplay.Abilities;

/// <summary>
/// WaitGameplayEvent Task Component —— 存储等待的 EventId。<br/>
/// 当 EventSystem 分发匹配的 GameplayEvent 时，TaskState 自动设为 Done。
/// </summary>
public struct WaitGameplayEventComponent : IComponent
{
    /// <summary>要监听的事件 ID。</summary>
    public ushort EventId;
}

/// <summary>
/// WaitGameplayEvent Task System —— 管理 Task 的注册和事件分发。<br/>
/// 1. 为 Pending Task 注册 EventSystem 动态 Listener。<br/>
/// 2. 通过 OnDynamicInvoke 回调，在匹配事件到达时将 TaskState 设为 Done。
/// </summary>
public class WaitGameplayEventTaskSystem : QuerySystem<WaitGameplayEventComponent, TaskStateComponent>
{
    private readonly EventSystem eventSystem;
    private readonly EntityStore store;
    private bool callbackRegistered;

    public WaitGameplayEventTaskSystem(EventSystem eventSystem, EntityStore store)
    {
        this.eventSystem = eventSystem;
        this.store = store;
    }

    protected override void OnUpdate()
    {
        // 注册动态分发回调（仅一次）
        if (!callbackRegistered)
        {
            eventSystem.OnDynamicInvoke += HandleDynamicInvoke;
            callbackRegistered = true;
        }

        // 为 Pending Task 注册 EventSystem 动态 Listener
        Query.ForEachEntity((ref WaitGameplayEventComponent wait, ref TaskStateComponent state, Entity entity) =>
        {
            if (state.State == TaskState.Pending)
            {
                eventSystem.RegisterDynamic(wait.EventId, entity, 0);
                state.State = TaskState.Running;
            }
        });
    }

    /// <summary>
    /// EventSystem 动态分发回调。
    /// 当事件 ID 匹配 WaitGameplayEventComponent.EventId 时，将 TaskState 设为 Done。
    /// </summary>
    private void HandleDynamicInvoke(in GameplayEventRecord record, int entityId, int handlerId)
    {
        var entity = store.GetEntityById(entityId);
        if (entity.IsNull)
            return;

        if (entity.TryGetComponent<WaitGameplayEventComponent>(out var waitComp))
        {
            if (waitComp.EventId == record.EventId)
            {
                ref var state = ref entity.GetComponent<TaskStateComponent>();
                state.State = TaskState.Done;
            }
        }
    }
}
