using Gameplay.Tags;

namespace Gameplay.Abilities;

/// <summary>效果查询条件——用于 Immunity、RemoveOtherEffects、GetActiveEffects 等。</summary>
public class GameplayEffectQuery
{
    public GameplayTagContainer OwningTagQuery = new();
    public GameplayTagContainer EffectTagQuery = new();
    public GameplayEffect? Definition;

    public bool IsEmpty =>
        OwningTagQuery.Count == 0 && EffectTagQuery.Count == 0 && Definition == null;

    public bool Matches(GameplayEffectSpec spec)
    {
        if (IsEmpty) return true;
        if (Definition != null && spec.Definition != Definition) return false;
        if (OwningTagQuery.Count > 0 && !spec.Definition.GrantedTags.HasAny(OwningTagQuery))
            return false;
        return true;
    }

    public static GameplayEffectQuery MakeQuery_MatchDefinition(GameplayEffect def)
        => new() { Definition = def };

    public static GameplayEffectQuery MakeQuery_MatchAnyGrantedTags(
        GameplayTagContainer tags) => new() { OwningTagQuery = tags };
}
