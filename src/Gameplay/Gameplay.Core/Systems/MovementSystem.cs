using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;

namespace Gameplay.Core;

/// <summary>速度积分（pos += vel * dt）。</summary>
public sealed class MovementSystem : QuerySystem<TransformComponent, VelocityComponent>
{
    private readonly ForEachEntity<TransformComponent, VelocityComponent> forEach;   // 缓存委托，避免每帧 this-capturing lambda 分配

    public MovementSystem() => forEach = ForEach;

    private void ForEach(ref TransformComponent transform, ref VelocityComponent velocity, Entity _)
    {
        transform.Position = transform.Position + velocity.Velocity * Tick.deltaTime;
    }

    protected override void OnUpdate()
    {
        Query.ForEachEntity(forEach);
    }
}
