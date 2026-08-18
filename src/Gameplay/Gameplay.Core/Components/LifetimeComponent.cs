using Friflo.Engine.ECS;

namespace Gameplay.Core;

/// <summary>存活倒计时（到期自动销毁）。</summary>
public struct LifetimeComponent : IComponent
{
    public float Remaining;
}
