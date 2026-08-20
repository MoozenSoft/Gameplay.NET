using Friflo.Engine.ECS;

namespace Gameplay.Replication;

/// <summary>shadow-diff 产出的 dirty 增量（实体 + 组件 typeId）。</summary>
internal readonly struct ReplicationDelta
{
    public readonly Entity Entity;
    public readonly int TypeId;

    public ReplicationDelta(Entity entity, int typeId)
    {
        Entity = entity;
        TypeId = typeId;
    }
}
