using Friflo.Engine.ECS;

namespace Gameplay.Abilities;

/// <summary>Commit 扩展点。Requirements 全部通过后执行，有副作用。</summary>
public interface IAbilityCommit
{
    void Execute(Entity owner, AbilitySpec spec, in AbilityActivationRequest request);
}
