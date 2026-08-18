using Friflo.Engine.ECS;

namespace Gameplay.Core;

/// <summary>归属玩家（未归属 = -1）。</summary>
public struct OwnerComponent : IComponent
{
    public int PlayerId;
}
