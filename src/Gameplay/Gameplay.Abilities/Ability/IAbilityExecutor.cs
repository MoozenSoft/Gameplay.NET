using Friflo.Engine.ECS;

namespace Gameplay.Abilities;

/// <summary>Execute 扩展点。能力逻辑的实际执行。</summary>
public interface IAbilityExecutor
{
    void Execute(Entity activeAbilityEntity, in AbilityActivationRequest request);
}
