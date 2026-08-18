using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;

namespace Gameplay.Core;

/// <summary>死亡判定：置 IsAlive=false → 广播 EntityDeathEvent → 延迟删除。</summary>
public sealed class HealthSystem : QuerySystem<HealthComponent>
{
    private readonly EventBus _events;

    public HealthSystem(EventBus events) => _events = events;

    protected override void OnUpdate()
    {
        var events = _events;
        Query.ForEachEntity((ref HealthComponent health, Entity entity) =>
        {
            if (!health.IsAlive || health.Current > 0f) return;
            health.IsAlive = false;   // 死亡中间态
            events.Enqueue(new EntityDeathEvent { Entity = entity });
            CommandBuffer.DeleteEntity(entity.Id);   // 经 CommandBuffer 帧末统一删除
        });
    }
}
