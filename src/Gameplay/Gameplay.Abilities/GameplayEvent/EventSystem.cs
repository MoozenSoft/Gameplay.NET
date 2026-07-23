using System.Collections.Generic;
using Friflo.Engine.ECS;

namespace Gameplay.Abilities;

/// <summary>
/// 事件消费系统。每帧 Tick 从 GameplayEventBus 取出待处理事件，
/// 分发给静态 Handler（IGameplayEventHandler）和动态 Listener（Entity 上的 Handler）。
/// </summary>
public class EventSystem
{
    private readonly GameplayEventBus bus;
    private readonly Dictionary<ushort, List<IGameplayEventHandler>> staticHandlers = new();
    private readonly Dictionary<ushort, List<(int entityId, int handlerId)>> dynamicListeners = new();

    public EventSystem(GameplayEventBus bus)
    {
        this.bus = bus;
    }

    /// <summary>注册静态 Handler，所有匹配事件都会调用该 Handler。</summary>
    public void RegisterStatic(ushort eventId, IGameplayEventHandler handler)
    {
        if (!staticHandlers.TryGetValue(eventId, out var list))
        {
            list = new List<IGameplayEventHandler>();
            staticHandlers[eventId] = list;
        }
        list.Add(handler);
    }

    /// <summary>注册动态 Listener（Entity 上的 Handler）。</summary>
    public void RegisterDynamic(ushort eventId, Entity owner, int handlerId)
    {
        if (!dynamicListeners.TryGetValue(eventId, out var list))
        {
            list = new List<(int, int)>();
            dynamicListeners[eventId] = list;
        }
        list.Add((owner.Id, handlerId));
    }

    /// <summary>注销动态 Listener。</summary>
    public void UnregisterDynamic(ushort eventId, Entity owner, int handlerId)
    {
        if (dynamicListeners.TryGetValue(eventId, out var list))
        {
            list.Remove((owner.Id, handlerId));
        }
    }

    /// <summary>
    /// 消费当前帧的所有事件：Swap 取出 pending 帧，分发给静态 Handler 和动态 Listener，然后 Reset。
    /// </summary>
    public void Tick()
    {
        var frame = bus.Swap();
        for (int i = 0; i < frame.Records.Count; i++)
        {
            ref var record = ref frame.Records.GetRef(i);

            // Dispatch to static handlers
            if (staticHandlers.TryGetValue(record.EventId, out var handlers))
            {
                foreach (var h in handlers)
                    h.Handle(record);
            }

            // Dispatch to dynamic listeners
            if (dynamicListeners.TryGetValue(record.EventId, out var listeners))
            {
                foreach (var (entityId, handlerId) in listeners)
                    InvokeDynamic(record, entityId, handlerId);
            }
        }
        frame.Reset();
    }

    /// <summary>动态 Handler 调用入口。子类可重写以实现具体的 Entity 上 Handler 调用逻辑。</summary>
    protected virtual void InvokeDynamic(in GameplayEventRecord record, int entityId, int handlerId)
    {
        // Placeholder: 后续 Plan 实现 Entity 上 Component Handler 调用
    }
}
