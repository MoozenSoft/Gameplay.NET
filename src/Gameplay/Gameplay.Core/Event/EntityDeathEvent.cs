using Friflo.Engine.ECS;

namespace Gameplay.Core;

/// <summary>实体死亡事件。</summary>
/// <remarks>World 采用双 Tick——死亡事件在 Simulation 入队、本帧第二次 Events.Tick 即分发，此时实体仍存活
/// （尚未删除），消费者可安全读取组件；实体在帧末 ProcessPendingDeletions 才真正删除。</remarks>
public struct EntityDeathEvent : IEvent
{
    /// <summary>死亡的实体（分发时仍存活，可读取组件）。</summary>
    public Entity Entity;

    /// <summary>击杀者（无击杀者为 null）。</summary>
    public Entity Killer;
}
