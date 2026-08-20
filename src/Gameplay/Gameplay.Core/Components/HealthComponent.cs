using Friflo.Engine.ECS;
using Gameplay;

namespace Gameplay.Core;

/// <summary>通用生命值 + 存活标记（死亡中间态）。</summary>
[Replicated]
public struct HealthComponent : IComponent
{
    public float Current;
    public float Max;
    public bool IsAlive;
}
