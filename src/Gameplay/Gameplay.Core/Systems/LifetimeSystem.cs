using System;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;

namespace Gameplay.Core;

/// <summary>存活倒计时，到期延迟删除（帧末 World.ProcessPendingDeletions 统一执行）。</summary>
public sealed class LifetimeSystem : QuerySystem<LifetimeComponent>
{
    private readonly ForEachEntity<LifetimeComponent> _forEach;   // 缓存委托，避免每帧 this-capturing lambda 分配
    private readonly Action<Entity> _deferDelete;

    public LifetimeSystem(Action<Entity> deferDelete)
    {
        _deferDelete = deferDelete;
        _forEach = ForEach;
    }

    private void ForEach(ref LifetimeComponent lifetime, Entity entity)
    {
        lifetime.Remaining -= Tick.deltaTime;
        if (lifetime.Remaining <= 0f)
            _deferDelete(entity);
    }

    protected override void OnUpdate()
    {
        Query.ForEachEntity(_forEach);
    }
}
