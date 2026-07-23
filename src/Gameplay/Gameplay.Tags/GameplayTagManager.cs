using System;
using System.Collections.Generic;

namespace Gameplay.Tags;

/// <summary>GameplayTag 全局注册中心。启动时注册，运行时只读。</summary>
public static class GameplayTagManager
{
    private static Dictionary<string, int> nameToId
        = new Dictionary<string, int>();

    private static GameplayTagNode[] nodes
        = Array.Empty<GameplayTagNode>();

    private static GameplayTagSet[] expandedSets
        = Array.Empty<GameplayTagSet>();

    private static bool dirty;

    /// <summary>
    /// 批量注册。唯一创建 GameplayTag 的入口。子 Tag 注册时自动创建缺失的父节点。
    /// </summary>
    public static void RegisterTags(params string[] tagNames)
    {
        if (tagNames == null || tagNames.Length == 0) return;

        // === 第一遍：解析并统计所有新增 Tag ===
        var newNames = new List<string>();
        foreach (var raw in tagNames)
        {
            var tagName = raw.Trim();
            if (string.IsNullOrEmpty(tagName))
                throw new ArgumentException("Tag 名不能为空", nameof(tagNames));
            if (tagName.StartsWith(".") || tagName.EndsWith("."))
                throw new ArgumentException($"Tag 名不能以点开头或结尾: '{tagName}'", nameof(tagNames));
            if (tagName.Contains(".."))
                throw new ArgumentException($"Tag 名不能包含连续点: '{tagName}'", nameof(tagNames));

            if (nameToId.ContainsKey(tagName)) continue; // 幂等

            // 检查所有中间父节点是否也需要新建
            string[] parts = tagName.Split('.');
            for (int i = 0; i < parts.Length; i++)
            {
                string fullName = string.Join(".", parts, 0, i + 1);
                if (!nameToId.ContainsKey(fullName) && !newNames.Contains(fullName))
                {
                    newNames.Add(fullName);
                }
            }
        }

        if (newNames.Count == 0) return;

        // === 一次性扩容数组 ===
        int newTotal = nameToId.Count + newNames.Count;
        if (newTotal >= nodes.Length)
        {
            int newSize = Math.Max(newTotal + 1, nodes.Length * 2);
            if (newSize < 8) newSize = 8;
            Array.Resize(ref nodes, newSize);
        }

        // === 第二遍：构建树节点 ===
        foreach (var fullName in newNames)
        {
            string[] parts = fullName.Split('.');
            GameplayTagNode? parent = null;

            for (int i = 0; i < parts.Length; i++)
            {
                string partial = string.Join(".", parts, 0, i + 1);
                if (nameToId.TryGetValue(partial, out int existingId))
                {
                    parent = nodes[existingId];
                }
                else
                {
                    int newId = nameToId.Count + 1;
                    var node = new GameplayTagNode(parts[i], partial, newId);
                    node.Parent = parent;
                    parent?.Children.Add(node);
                    nodes[newId] = node;
                    nameToId[partial] = newId;
                    parent = node;
                    dirty = true;
                }
            }
        }
    }

    /// <summary>获取已注册的 GameplayTag。只读，不存在则返回 Invalid。</summary>
    public static GameplayTag RequestTag(string tagName)
    {
        if (dirty) Build();
        if (nameToId.TryGetValue(tagName, out int id))
            return new GameplayTag(id);
        return GameplayTag.Invalid;
    }

    // ---- 内部 ----

    internal static int TagCount => nameToId.Count;

    internal static string? GetName(int id)
        => id > 0 && id < nodes.Length && nodes[id] != null
            ? nodes[id].FullName : null;

    internal static ReadOnlySpan<long> GetExpandedSet(int id)
    {
        if (dirty) Build();
        if (id > 0 && id < expandedSets.Length)
            return expandedSets[id].Bits;
        return ReadOnlySpan<long>.Empty;
    }

    internal static bool Matches(int childId, int parentId)
    {
        if (dirty) Build();
        if (childId <= 0 || parentId <= 0) return false;
        if (parentId >= expandedSets.Length) return false;
        return expandedSets[parentId].Has(childId);
    }

    internal static void Build()
    {
        if (!dirty) return;
        int count = nameToId.Count;
        if (count == 0) { dirty = false; return; }

        expandedSets = new GameplayTagSet[count + 1];

        // 初始化：每个节点展开集 = {自身}
        for (int id = 1; id <= count; id++)
        {
            if (nodes[id] != null)
                expandedSets[id].Set(id);
        }

        // 自底向上传播：子节点展开集合并到父节点
        // （子节点 id 总是大于父节点，倒序遍历确保孩子先于父亲处理）
        for (int id = count; id >= 1; id--)
        {
            var node = nodes[id];
            if (node?.Parent != null)
            {
                ref var parentSet = ref expandedSets[node.Parent.Id];
                ref var childSet  = ref expandedSets[id];
                MergeExpanded(ref parentSet, childSet);
            }
        }

        dirty = false;
    }

    private static void MergeExpanded(ref GameplayTagSet target, in GameplayTagSet source)
    {
        var srcBits = source.Bits;
        if (srcBits.Length == 0) return;
        for (int i = 0; i < srcBits.Length; i++)
        {
            long word = srcBits[i];
            if (word == 0) continue;
            for (int bit = 0; bit < 64; bit++)
            {
                if ((word & (1L << bit)) != 0)
                {
                    target.Set(i * 64 + bit);
                }
            }
        }
    }
}
