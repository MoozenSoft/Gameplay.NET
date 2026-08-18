using Friflo.Engine.ECS;

namespace Gameplay.Core;

/// <summary>空间变换。</summary>
public struct TransformComponent : IComponent
{
    public Vector3 Position;
    public Quaternion Rotation;
    public float Scale;
}
