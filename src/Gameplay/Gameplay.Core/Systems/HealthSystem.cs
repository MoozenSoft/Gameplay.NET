using System;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;

namespace Gameplay.Core;

/// <summary>死亡判定：Current &lt;= 0 → 置 IsAlive=false（死亡中间态）→ 广播 EntityDeathEvent → 延迟删除。
/// 注意：World 采用双 Tick——死亡事件在 Simulation 入队、本帧第二次 Events.Tick 即分发（此时实体仍存活，
/// 消费者可安全读取组件）；实体在帧末 ProcessPendingDeletions 才真正删除。</summary>
public sealed class HealthSystem : QuerySystem<HealthComponent>
{
    private readonly ForEachEntity<HealthComponent> forEach;   // 缓存委托，避免每帧 this-capturing lambda 分配
    private readonly EventBus events;
    private readonly Action<Entity> deferDelete;

    public HealthSystem(EventBus events, Action<Entity> deferDelete)
    {
        this.events = events;
        this.deferDelete = deferDelete;
        forEach = ForEach;
    }

    private void ForEach(ref HealthComponent health, Entity entity)
    {
        // 死亡判定基于 Current（不依赖 IsAlive 初始值——IsAlive 缺省为 false 的实体也会正确判定）
        if (health.Current > 0f) return;
        health.IsAlive = false;   // 死亡中间态
        events.Enqueue(new EntityDeathEvent { Entity = entity });
        deferDelete(entity);
    }

    protected override void OnUpdate()
    {
        Query.ForEachEntity(forEach);
    }
}
