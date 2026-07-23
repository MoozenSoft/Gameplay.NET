using Friflo.Engine.ECS;

namespace Gameplay.Abilities;

/// <summary>CanActivate 检查扩展点。返回 true = 通过。</summary>
public interface IAbilityRequirement
{
    bool Evaluate(Entity owner, AbilitySpec spec, in AbilityActivationRequest request);
}
