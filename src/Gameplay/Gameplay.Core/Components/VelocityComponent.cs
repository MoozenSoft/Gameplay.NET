using Friflo.Engine.ECS;

namespace Gameplay.Core;

/// <summary>速度（供 MovementSystem 积分）。</summary>
public struct VelocityComponent : IComponent
{
    public Vector3 Velocity;
}
