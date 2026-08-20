using Friflo.Engine.ECS;
using Gameplay;

namespace Gameplay.Core;

/// <summary>归属玩家（未归属 = -1）。</summary>
[Replicated]
public struct OwnerComponent : IComponent
{
    public int PlayerId;
}
