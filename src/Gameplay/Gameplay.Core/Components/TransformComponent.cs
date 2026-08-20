using Friflo.Engine.ECS;
using Gameplay;

namespace Gameplay.Core;

/// <summary>空间变换。</summary>
[Replicated]
public struct TransformComponent : IComponent
{
    public Vector3 Position;
    public Quaternion Rotation;
    public float Scale;
}
