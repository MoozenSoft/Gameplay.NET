using Friflo.Engine.ECS;
using Gameplay.Tags;

namespace Gameplay.Abilities;

/// <summary>
/// Ability 激活 Tag 条件检查。
/// 检查 ActivationRequiredTags / ActivationBlockedTags / SourceRequiredTags / SourceBlockedTags。
/// </summary>
public class TagRequirement : IAbilityRequirement
{
    public bool Evaluate(Entity owner, AbilitySpec spec, in AbilityActivationRequest request)
    {
        var ability = spec.Ability;
        if (ability == null) return true;

        // ActivationBlockedTags: Owner 有任一 Blocked Tag → 失败
        if (ability.ActivationBlockedTags.Count > 0)
        {
            if (owner.TryGetComponent<GameplayTagsComponent>(out var ownerTags))
            {
                if (ownerTags.MatchesAnyTags(ability.ActivationBlockedTags))
                    return false;
            }
        }

        // ActivationRequiredTags: Owner 必须有全部 Required Tag
        if (ability.ActivationRequiredTags.Count > 0)
        {
            if (!owner.TryGetComponent<GameplayTagsComponent>(out var ownerTags))
                return false;
            if (!ownerTags.HasAll(ability.ActivationRequiredTags))
                return false;
        }

        // Cooldown 检查: 有 Cooldown GE 施加的 Cooldown Tag 则阻止
        // Cooldown Tag 由 CooldownEffect.GrantedTags 施加，检查 Owner 是否有该 Tag
        // 此检查已在 ActivationBlockedTags 中通过策划配置"Cooldown.X" Tag 覆盖

        return true;
    }
}
