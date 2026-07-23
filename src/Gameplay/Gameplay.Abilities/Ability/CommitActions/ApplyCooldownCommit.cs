using Friflo.Engine.ECS;

namespace Gameplay.Abilities;

/// <summary>施加 Cooldown GameplayEffect 的 Commit。</summary>
public class ApplyCooldownCommit : IAbilityCommit
{
    private readonly EffectSystem effectSystem;

    public ApplyCooldownCommit(EffectSystem effectSystem)
    {
        this.effectSystem = effectSystem;
    }

    public void Execute(Entity owner, AbilitySpec spec, in AbilityActivationRequest request)
    {
        if (spec.Ability.CooldownEffect == null) return;
        var geSpec = new GameplayEffectSpec(spec.Ability.CooldownEffect, spec.Level);
        effectSystem.Apply(geSpec, owner);
    }
}
