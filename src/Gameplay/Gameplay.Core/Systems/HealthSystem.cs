using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;

namespace Gameplay.Core;

/// <summary>死亡判定：置 IsAlive=false → 广播 EntityDeathEvent → 延迟删除。
/// 注意：死亡事件在 Simulation 入队、下一帧 Events.Tick 分发，但实体已由 CommandBuffer 在入队当帧回放删除，
/// 消费者收到 EntityDeathEvent 时 Entity 已死——应只读 Entity.Id，不得读取组件。</summary>
public sealed class HealthSystem : QuerySystem<HealthComponent>
{
    private readonly EventBus _events;

    public HealthSystem(EventBus events) => _events = events;

    protected override void OnUpdate()
    {
        Query.ForEachEntity((ref HealthComponent health, Entity entity) =>
        {
            if (!health.IsAlive || health.Current > 0f) return;
            health.IsAlive = false;   // 死亡中间态
            _events.Enqueue(new EntityDeathEvent { Entity = entity });
            CommandBuffer.DeleteEntity(entity.Id);   // 经 CommandBuffer 帧末统一删除
        });
    }
}
