using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;

namespace Gameplay.Core;

/// <summary>存活倒计时，到期销毁。</summary>
public sealed class LifetimeSystem : QuerySystem<LifetimeComponent>
{
    protected override void OnUpdate()
    {
        Query.ForEachEntity((ref LifetimeComponent lifetime, Entity entity) =>
        {
            lifetime.Remaining -= Tick.deltaTime;
            if (lifetime.Remaining <= 0f)
                CommandBuffer.DeleteEntity(entity.Id);   // 经 CommandBuffer 帧末统一删除
        });
    }
}
