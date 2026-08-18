using Friflo.Engine.ECS;

namespace Gameplay.Core;

/// <summary>实体死亡事件。</summary>
/// <remarks>死亡事件在 Simulation 入队、下一帧 Events.Tick 分发；但实体删除已通过 CommandBuffer 在入队当帧回放，
/// 消费者收到事件时 Entity 已被删除——应只读 Entity.Id，不得读取组件。</remarks>
public struct EntityDeathEvent : IEvent
{
    /// <summary>死亡的实体（分发时已被删除，仅可读取 Id）。</summary>
    public Entity Entity;

    /// <summary>击杀者（无击杀者为 null）。</summary>
    public Entity Killer;
}
