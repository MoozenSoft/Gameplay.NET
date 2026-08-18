using Friflo.Engine.ECS;

namespace Gameplay.Core;

/// <summary>实体死亡事件。</summary>
public struct EntityDeathEvent : IEvent
{
    /// <summary>死亡的实体。</summary>
    public Entity Entity;

    /// <summary>击杀者（无击杀者为 null）。</summary>
    public Entity Killer;
}
