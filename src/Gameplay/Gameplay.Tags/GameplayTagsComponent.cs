using Friflo.Engine.ECS;

namespace Gameplay.Tags;

/// <summary>附加到 Entity 的 GameplayTag 运行时集合。</summary>
public struct GameplayTagsComponent : IComponent
{
    internal GameplayTagSet tagSet;

    public int Count => tagSet.Count;

    public void AddTag(GameplayTag tag)    => tagSet.Set(tag.id);
    public void RemoveTag(GameplayTag tag) => tagSet.Clear(tag.id);

    public bool HasTag(GameplayTag tag)    => tagSet.Has(tag.id);

    public bool HasAll(GameplayTagContainer required) => tagSet.HasAll(required.tagSet);

    public bool Matches(GameplayTag tag)
        => tagSet.HasAny(GameplayTagManager.GetExpandedSet(tag.id));

    public bool MatchesAnyTags(GameplayTagContainer container) => tagSet.HasAny(container.tagSet);

    public bool MatchesAny(GameplayTagsComponent other)
    {
        // 快速路径：直接位重叠（精确 Tag 匹配）
        if (tagSet.HasAny(other.tagSet))
            return true;

        // 层级匹配：是否存在某个 Tag，其展开集与两个集合均有重叠
        int maxId = GameplayTagManager.TagCount;
        if (maxId <= 0) return false;

        for (int id = 1; id <= maxId; id++)
        {
            var expanded = GameplayTagManager.GetExpandedSet(id);
            if (expanded.Length > 0
                && tagSet.HasAny(expanded)
                && other.tagSet.HasAny(expanded))
            {
                return true;
            }
        }

        return false;
    }
}
