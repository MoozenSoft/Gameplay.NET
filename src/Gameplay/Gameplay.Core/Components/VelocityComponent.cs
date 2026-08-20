using Friflo.Engine.ECS;
using Gameplay;

namespace Gameplay.Core;

/// <summary>速度（供 MovementSystem 积分）。</summary>
[Replicated]
public struct VelocityComponent : IComponent
{
    public Vector3 Velocity;
}
