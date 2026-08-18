using Friflo.Engine.ECS;

namespace Gameplay.Core;

/// <summary>通用计时/冷却。</summary>
public struct TimerComponent : IComponent
{
    public float Remaining;
    public float Duration;
    public bool Loop;
    public bool Completed;
}
