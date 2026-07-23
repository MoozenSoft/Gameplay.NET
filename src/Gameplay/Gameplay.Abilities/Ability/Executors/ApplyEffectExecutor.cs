using Friflo.Engine.ECS;

namespace Gameplay.Abilities;

/// <summary>
/// 对 Target 施加一组 GameplayEffectSpec 的 Executor。
/// 典型的"火球 = Apply DamageGE(to target) + Apply BurnEffect(to target)"模式。
/// </summary>
public class ApplyEffectExecutor : IAbilityExecutor
{
    private readonly EffectSystem effectSystem;
    private readonly GameplayEffectSpec[] effectSpecs;

    /// <param name="specs">要施加的 GE Spec 列表。</param>
    public ApplyEffectExecutor(EffectSystem effectSys, params GameplayEffectSpec[] specs)
    {
        effectSystem = effectSys;
        effectSpecs = specs;
    }

    public void Execute(Entity activeAbilityEntity, in AbilityActivationRequest request)
    {
        foreach (var spec in effectSpecs)
        {
            effectSystem.Apply(spec, request.Target);
        }
    }
}
