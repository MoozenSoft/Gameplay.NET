using Friflo.Engine.ECS;

namespace Gameplay.Core;

/// <summary>一次性生成点（生成后移除）。</summary>
public struct SpawnPointComponent : IComponent
{
    public int PrefabId;
    public int TeamId;
}
