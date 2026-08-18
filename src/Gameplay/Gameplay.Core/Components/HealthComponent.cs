using Friflo.Engine.ECS;

namespace Gameplay.Core;

/// <summary>通用生命值 + 存活标记（死亡中间态）。</summary>
public struct HealthComponent : IComponent
{
    public float Current;
    public float Max;
    public bool IsAlive;
}
