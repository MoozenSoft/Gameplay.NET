using Friflo.Engine.ECS;

namespace Gameplay.Core;

/// <summary>阵营（未组队 = 0）。</summary>
public struct TeamComponent : IComponent
{
    public int TeamId;
}
