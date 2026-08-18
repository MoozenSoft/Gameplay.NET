using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;

namespace Gameplay.Core;

/// <summary>速度积分（pos += vel * dt）。</summary>
public sealed class MovementSystem : QuerySystem<TransformComponent, VelocityComponent>
{
    protected override void OnUpdate()
    {
        var dt = Tick.deltaTime;
        Query.ForEachEntity((ref TransformComponent transform, ref VelocityComponent velocity, Entity _) =>
        {
            transform.Position = transform.Position + velocity.Velocity * dt;
        });
    }
}
