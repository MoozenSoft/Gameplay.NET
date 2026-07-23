using Friflo.Engine.ECS;

namespace Gameplay.Abilities;

/// <summary>Ability 激活请求（POCO Command）。当前 Tick 消费。</summary>
public struct AbilityActivationRequest
{
    public Entity Owner;
    public int SpecHandle;
    public Entity Target;
    public ActivationSource Source;
}
