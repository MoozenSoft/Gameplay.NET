using System.Collections.Generic;
using Friflo.Engine.ECS;

namespace Gameplay.Tags;

/// <summary>附加到 Entity 的 GameplayTag 运行时集合，支持引用计数。</summary>
public struct GameplayTagsComponent : IComponent
{
    internal GameplayTagSet tagSet;
    internal Dictionary<GameplayTag, int>? refCounts;

    public int Count => tagSet.Count;

    /// <summary>添加 Tag，支持多个来源独立 Add/Remove。仅当引用计数从 0 变为 1 时设置位。</summary>
    public void AddTag(GameplayTag tag)
    {
        refCounts ??= new Dictionary<GameplayTag, int>();
        refCounts.TryGetValue(tag, out int count);
        refCounts[tag] = count + 1;
        if (count == 0)
            tagSet.Set(tag.id);
    }

    /// <summary>移除 Tag。仅当引用计数降为 0 时清除位。</summary>
    public void RemoveTag(GameplayTag tag)
    {
        if (refCounts == null || !refCounts.TryGetValue(tag, out int count) || count <= 0)
            return;
        int newCount = count - 1;
        if (newCount == 0)
        {
            refCounts.Remove(tag);
            tagSet.Clear(tag.id);
        }
        else
        {
            refCounts[tag] = newCount;
        }
    }

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
