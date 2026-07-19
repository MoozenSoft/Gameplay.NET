# GameplayTag 实现计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 实现 GameplayTag 层级标签系统——GameplayTagSet 位集、GameplayTagManager 注册中心、GameplayTag 句柄、GameplayTags ECS Component

**Architecture:** 自底向上 TDD。先 GameplayTagSet（纯数据结构），再 GameplayTagNode + GameplayTagManager（注册与展开集），再 GameplayTag（句柄），最后 GameplayTags（ECS Component）。每个 Task 先写测试、确认失败、再实现、确认通过、提交。

**Tech Stack:** C# LangVersion 12, netstandard2.1 + net10.0, Friflo.Engine.ECS 3.x, xUnit

## Global Constraints

- TargetFrameworks: `netstandard2.1;net10.0`
- LangVersion: 12
- Nullable: enable
- 文件范围命名空间：`namespace Gameplay;`
- 注释和文档使用中文
- 0 GC 优先（struct 代替 class，热路径用位运算）
- TDD：先写测试 → 确认失败 → 实现 → 确认通过 → 提交

---

### Task 1: GameplayTagSet — 可扩展位集

**Files:**
- Create: `src/Gameplay/GameplayTags/GameplayTagSet.cs`
- Test: `tests/Gameplay.Tests/GameplayTags/GameplayTagSetTests.cs`

**Interfaces:**
- Produces: `internal struct GameplayTagSet` with `Set(int)`, `Clear(int)`, `Has(int)`, `HasAny(ReadOnlySpan<long>)`, `HasAny(in GameplayTagSet)`, `Count`, `Bits`

- [ ] **Step 1: 创建测试目录和测试文件**

```csharp
// tests/Gameplay.Tests/GameplayTags/GameplayTagSetTests.cs
using Xunit;

namespace Gameplay.Tests;

public class GameplayTagSetTests
{
    [Fact]
    public void Set_Has_ReturnsTrue_AfterSet()
    {
        var set = new GameplayTagSet();
        set.Set(1);
        Assert.True(set.Has(1));
        Assert.False(set.Has(2));
    }

    [Fact]
    public void Clear_Has_ReturnsFalse_AfterClear()
    {
        var set = new GameplayTagSet();
        set.Set(1);
        set.Clear(1);
        Assert.False(set.Has(1));
    }

    [Fact]
    public void Count_ReturnsCorrectCount()
    {
        var set = new GameplayTagSet();
        Assert.Equal(0, set.Count);
        set.Set(1);
        Assert.Equal(1, set.Count);
        set.Set(3);
        Assert.Equal(2, set.Count);
        set.Set(1); // 重复置位不增 count
        Assert.Equal(2, set.Count);
        set.Clear(1);
        Assert.Equal(1, set.Count);
    }

    [Fact]
    public void HasAny_WithExpandedSet_ReturnsTrue_WhenIntersectionExists()
    {
        var set = new GameplayTagSet();
        set.Set(2); // Damage.Fire
        set.Set(3); // Damage.Ice

        // Damage 的展开集 = {1, 2, 3}
        long[] expanded = new long[1];
        expanded[0] = (1L << 1) | (1L << 2) | (1L << 3);

        Assert.True(set.HasAny(((ReadOnlySpan<long>)expanded)));
    }

    [Fact]
    public void HasAny_WithExpandedSet_ReturnsFalse_WhenNoIntersection()
    {
        var set = new GameplayTagSet();
        set.Set(5); // Buff.Regeneration

        // Damage 的展开集 = {1, 2, 3}
        long[] expanded = new long[1];
        expanded[0] = (1L << 1) | (1L << 2) | (1L << 3);

        Assert.False(set.HasAny(((ReadOnlySpan<long>)expanded)));
    }

    [Fact]
    public void LargeId_ExpandsArrayAndWorks()
    {
        var set = new GameplayTagSet();
        // id=100 → index=1 (100/64), bit=36 (100%64)
        set.Set(100);
        Assert.True(set.Has(100));
        Assert.Equal(1, set.Count);
    }

    [Fact]
    public void HasAny_BetweenTwoSets_ReturnsCorrectResult()
    {
        var a = new GameplayTagSet();
        a.Set(1);
        a.Set(2);

        var b = new GameplayTagSet();
        b.Set(2);
        b.Set(3);

        Assert.True(a.HasAny(b));
    }

    [Fact]
    public void HasAll_ReturnsTrue_WhenAllBitsPresent()
    {
        var a = new GameplayTagSet();
        a.Set(1);
        a.Set(2);
        a.Set(3);

        var b = new GameplayTagSet();
        b.Set(1);
        b.Set(3);

        Assert.True(a.HasAll(b));
    }

    [Fact]
    public void HasAll_ReturnsFalse_WhenBitsMissing()
    {
        var a = new GameplayTagSet();
        a.Set(1);

        var b = new GameplayTagSet();
        b.Set(1);
        b.Set(2);

        Assert.False(a.HasAll(b));
    }
}
```

- [ ] **Step 2: 创建 GameplayTags 源码目录**

```bash
mkdir -p src/Gameplay/GameplayTags
```

- [ ] **Step 3: 运行测试确认失败**

```bash
dotnet test tests/Gameplay.Tests/Gameplay.Tests.csproj --filter "FullyQualifiedName~GameplayTagSetTests"
```
预期：编译失败，`GameplayTagSet` 类型不存在。

- [ ] **Step 4: 实现 GameplayTagSet**

```csharp
// src/Gameplay/GameplayTags/GameplayTagSet.cs
using System;

namespace Gameplay;

/// <summary>内部可扩展位集，用 long[] 存储任意数量的 Tag 位。</summary>
internal struct GameplayTagSet
{
    private long[] bits;   // null = 空集（延迟分配）
    private int    count;  // 已设置的 bit 数量

    public int Count => count;

    internal ReadOnlySpan<long> Bits => bits;

    public void Set(int id)
    {
        int index = id >> 6;          // id / 64
        long mask = 1L << (id & 63);  // id % 64
        EnsureCapacity(index);
        if ((bits[index] & mask) == 0)
        {
            count++;
        }
        bits[index] |= mask;
    }

    public void Clear(int id)
    {
        if (bits == null) return;
        int index = id >> 6;
        long mask = 1L << (id & 63);
        if (index < bits.Length && (bits[index] & mask) != 0)
        {
            count--;
            bits[index] &= ~mask;
        }
    }

    public bool Has(int id)
    {
        if (bits == null) return false;
        int index = id >> 6;
        if (index >= bits.Length) return false;
        long mask = 1L << (id & 63);
        return (bits[index] & mask) != 0;
    }

    public bool HasAny(in GameplayTagSet other)
        => HasAny(other.Bits);

    public bool HasAny(ReadOnlySpan<long> expandedSet)
    {
        if (bits == null || expandedSet.Length == 0) return false;
        int minLen = Math.Min(bits.Length, expandedSet.Length);
        for (int i = 0; i < minLen; i++)
        {
            if ((bits[i] & expandedSet[i]) != 0) return true;
        }
        return false;
    }

    public bool HasAll(in GameplayTagSet other)
    {
        if (other.bits == null) return true;
        if (bits == null) return false;
        if (other.bits.Length > bits.Length) return false;
        for (int i = 0; i < other.bits.Length; i++)
        {
            if ((bits[i] & other.bits[i]) != other.bits[i]) return false;
        }
        return true;
    }

    private void EnsureCapacity(int index)
    {
        if (bits == null)
        {
            bits = new long[index + 1];
            return;
        }
        if (index >= bits.Length)
        {
            int newSize = index + 1;
            Array.Resize(ref bits, newSize);
        }
    }
}
```

- [ ] **Step 5: 运行测试确认通过**

```bash
dotnet test tests/Gameplay.Tests/Gameplay.Tests.csproj --filter "FullyQualifiedName~GameplayTagSetTests"
```
预期：全部 PASS（9 个测试）。

- [ ] **Step 6: 提交**

```bash
git add src/Gameplay/GameplayTags/GameplayTagSet.cs tests/Gameplay.Tests/GameplayTags/GameplayTagSetTests.cs
git commit -m "feat: GameplayTagSet 可扩展位集"
```

---

### Task 2: GameplayTagNode — 层级树节点

**Files:**
- Create: `src/Gameplay/GameplayTags/GameplayTagNode.cs`

**Interfaces:**
- Produces: `internal sealed class GameplayTagNode` with `Name`, `FullName`, `Id`, `Parent`, `Children`

- [ ] **Step 1: 实现 GameplayTagNode（无独立测试，被 Manager 测试间接覆盖）**

```csharp
// src/Gameplay/GameplayTags/GameplayTagNode.cs
using System.Collections.Generic;

namespace Gameplay;

/// <summary>GameplayTag 层级树节点，仅供 GameplayTagManager 内部使用。</summary>
internal sealed class GameplayTagNode
{
    internal readonly string Name;     // 短名（如 "Fire"）
    internal readonly string FullName; // 全名（如 "Damage.Fire"）
    internal readonly int    Id;

    internal GameplayTagNode          Parent;
    internal List<GameplayTagNode>    Children;

    internal GameplayTagNode(string name, string fullName, int id)
    {
        Name     = name;
        FullName = fullName;
        Id       = id;
        Children = new List<GameplayTagNode>();
    }
}
```

- [ ] **Step 2: 编译验证**

```bash
dotnet build src/Gameplay/Gameplay.csproj
```
预期：BUILD SUCCESS。

- [ ] **Step 3: 提交**

```bash
git add src/Gameplay/GameplayTags/GameplayTagNode.cs
git commit -m "feat: GameplayTagNode 层级树节点"
```

---

### Task 3: GameplayTagManager + GameplayTag — 注册中心与句柄

**Files:**
- Create: `src/Gameplay/GameplayTags/GameplayTagManager.cs`
- Create: `src/Gameplay/GameplayTags/GameplayTag.cs`
- Test: `tests/Gameplay.Tests/GameplayTags/GameplayTagManagerTests.cs`

**Interfaces:**
- Consumes: `GameplayTagSet`, `GameplayTagNode`
- Produces: `public static class GameplayTagManager` with `RegisterTags`, `RequestTag`, `Build`, `GetName`, `GetExpandedSet`, `Matches`
- Produces: `public readonly struct GameplayTag : IEquatable<GameplayTag>` with `Request`, `Matches`, `MatchesExact`, `IsValid`, `id`

- [ ] **Step 1: 写 Manager 注册与查询测试（先写会编译失败的测试部分）**

```csharp
// tests/Gameplay.Tests/GameplayTags/GameplayTagManagerTests.cs
using Xunit;

namespace Gameplay.Tests;

public class GameplayTagManagerTests
{
    [Fact]
    public void RegisterTags_CreatesHierarchy()
    {
        // Build 应由首次 RequestTag 自动触发
        GameplayTagManager.RegisterTags("Damage", "Damage.Fire");

        var fireTag = GameplayTag.Request("Damage.Fire");
        Assert.True(fireTag.IsValid);

        var damageTag = GameplayTag.Request("Damage");
        Assert.True(damageTag.IsValid);

        // Damage.Fire 是 Damage 的子孙
        Assert.True(fireTag.Matches(damageTag));
    }

    [Fact]
    public void RegisterTags_AutoCreatesParentNodes()
    {
        // 只注册叶子节点，父节点自动创建
        GameplayTagManager.RegisterTags("A.B.C");

        var aTag = GameplayTag.Request("A");
        var bTag = GameplayTag.Request("A.B");
        var cTag = GameplayTag.Request("A.B.C");

        Assert.True(aTag.IsValid);
        Assert.True(bTag.IsValid);
        Assert.True(cTag.IsValid);
        Assert.True(cTag.Matches(aTag));
    }

    [Fact]
    public void RegisterTags_DuplicateIsIdempotent()
    {
        GameplayTagManager.RegisterTags("Damage");
        var tag1 = GameplayTag.Request("Damage");
        GameplayTagManager.RegisterTags("Damage");
        var tag2 = GameplayTag.Request("Damage");
        Assert.Equal(tag1, tag2);
    }

    [Fact]
    public void RequestTag_ReturnsInvalid_WhenNotRegistered()
    {
        // RequestTag 不创建——必须返回 Invalid
        var tag = GameplayTag.Request("Not.Exists");
        Assert.False(tag.IsValid);
    }

    [Fact]
    public void Matches_ParentMatchesChild()
    {
        GameplayTagManager.RegisterTags("Damage", "Damage.Fire");
        var fire = GameplayTag.Request("Damage.Fire");
        var damage = GameplayTag.Request("Damage");
        Assert.True(fire.Matches(damage));
    }

    [Fact]
    public void Matches_ChildDoesNotMatchParent()
    {
        GameplayTagManager.RegisterTags("Damage", "Damage.Fire");
        var fire = GameplayTag.Request("Damage.Fire");
        var damage = GameplayTag.Request("Damage");
        Assert.False(damage.Matches(fire));
    }

    [Fact]
    public void MatchesExact_SameIdReturnsTrue()
    {
        GameplayTagManager.RegisterTags("Damage");
        var a = GameplayTag.Request("Damage");
        var b = GameplayTag.Request("Damage");
        Assert.True(a.MatchesExact(b));
    }

    [Fact]
    public void Matches_SelfMatchesSelf()
    {
        GameplayTagManager.RegisterTags("Damage.Fire");
        var fire = GameplayTag.Request("Damage.Fire");
        Assert.True(fire.Matches(fire));
    }

    [Fact]
    public void RequestTag_ReturnsSameIdForSameName()
    {
        GameplayTagManager.RegisterTags("Damage", "Damage.Fire");
        var a = GameplayTag.Request("Damage.Fire");
        var b = GameplayTag.Request("Damage.Fire");
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
        Assert.True(a.Equals(b));
    }

    [Fact]
    public void InvalidTag_HasIdZero_AndIsNotValid()
    {
        var invalid = default(GameplayTag);
        Assert.False(invalid.IsValid);
        Assert.Equal("Invalid", invalid.ToString());
    }
}
```

- [ ] **Step 2: 运行测试确认失败**

```bash
dotnet test tests/Gameplay.Tests/Gameplay.Tests.csproj --filter "FullyQualifiedName~GameplayTagManagerTests"
```
预期：编译失败，`GameplayTagManager` 和 `GameplayTag` 类型不存在。

- [ ] **Step 3: 实现 GameplayTag**

```csharp
// src/Gameplay/GameplayTags/GameplayTag.cs
using System;

namespace Gameplay;

/// <summary>不可变的轻量 GameplayTag 句柄，包装一个 int id。</summary>
public readonly struct GameplayTag : IEquatable<GameplayTag>
{
    internal readonly int id;

    internal GameplayTag(int id) => this.id = id;

    /// <summary>id 为 0 表示无效 Tag。</summary>
    public static GameplayTag Invalid => default;

    public bool IsValid => id > 0;

    /// <summary>从已注册集合获取 GameplayTag。不存在则返回 Invalid。</summary>
    public static GameplayTag Request(string tagName)
        => GameplayTagManager.RequestTag(tagName);

    /// <summary>层级匹配：此 Tag 是否是 parent 自身或其子孙。</summary>
    public bool Matches(GameplayTag parent)
        => GameplayTagManager.Matches(id, parent.id);

    /// <summary>精确匹配（仅检查 id 相等）。</summary>
    public bool MatchesExact(GameplayTag other) => id == other.id;

    internal ReadOnlySpan<long> GetExpandedSet()
        => GameplayTagManager.GetExpandedSet(id);

    public bool Equals(GameplayTag other) => id == other.id;
    public override bool Equals(object obj) => obj is GameplayTag other && Equals(other);
    public override int GetHashCode() => id;

    public override string ToString()
        => IsValid ? GameplayTagManager.GetName(id) : "Invalid";
}
```

- [ ] **Step 4: 实现 GameplayTagManager**

```csharp
// src/Gameplay/GameplayTags/GameplayTagManager.cs
using System;
using System.Collections.Generic;

namespace Gameplay;

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
            GameplayTagNode parent = null;

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

    internal static string GetName(int id)
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
```

- [ ] **Step 5: 运行测试确认通过**

```bash
dotnet test tests/Gameplay.Tests/Gameplay.Tests.csproj --filter "FullyQualifiedName~GameplayTagManagerTests"
```
预期：全部 PASS（10 个测试）。

- [ ] **Step 6: 提交**

```bash
git add src/Gameplay/GameplayTags/GameplayTagManager.cs src/Gameplay/GameplayTags/GameplayTag.cs tests/Gameplay.Tests/GameplayTags/GameplayTagManagerTests.cs
git commit -m "feat: GameplayTagManager + GameplayTag — 注册与层级查询"
```

---

### Task 4: GameplayTags — ECS Component

**Files:**
- Create: `src/Gameplay/GameplayTags/GameplayTags.cs`
- Test: `tests/Gameplay.Tests/GameplayTags/GameplayTagsTests.cs`

**Interfaces:**
- Consumes: `GameplayTagSet`, `GameplayTag`, `GameplayTagManager`
- Produces: `public struct GameplayTags : IComponent` with `AddTag`, `RemoveTag`, `HasTag`, `Matches`, `MatchesAny`, `Count`

- [ ] **Step 1: 写 GameplayTags Component 测试**

```csharp
// tests/Gameplay.Tests/GameplayTags/GameplayTagsTests.cs
using Xunit;

namespace Gameplay.Tests;

public class GameplayTagsTests
{
    private World CreateWorld()
    {
        GameplayTagManager.RegisterTags(
            "Damage",
            "Damage.Fire",
            "Damage.Fire.DoT",
            "Damage.Ice",
            "StatusEffect.Stunned",
            "Buff.Regeneration"
        );
        return new World(NetMode.Standalone);
    }

    [Fact]
    public void AddTag_HasTag_ReturnsTrue()
    {
        var world = CreateWorld();
        var entity = world.Store.CreateEntity();
        entity.AddComponent(new GameplayTags());

        var fireTag = GameplayTag.Request("Damage.Fire");
        ref var tags = ref entity.GetComponent<GameplayTags>();
        tags.AddTag(fireTag);

        Assert.True(tags.HasTag(fireTag));
        Assert.Equal(1, tags.Count);
    }

    [Fact]
    public void RemoveTag_HasTag_ReturnsFalse()
    {
        var world = CreateWorld();
        var entity = world.Store.CreateEntity();
        entity.AddComponent(new GameplayTags());

        var fireTag = GameplayTag.Request("Damage.Fire");
        ref var tags = ref entity.GetComponent<GameplayTags>();
        tags.AddTag(fireTag);
        tags.RemoveTag(fireTag);

        Assert.False(tags.HasTag(fireTag));
        Assert.Equal(0, tags.Count);
    }

    [Fact]
    public void EmptyKeepsComponent_AfterAllTagsRemoved()
    {
        var world = CreateWorld();
        var entity = world.Store.CreateEntity();
        entity.AddComponent(new GameplayTags());

        var fireTag = GameplayTag.Request("Damage.Fire");
        ref var tags = ref entity.GetComponent<GameplayTags>();
        tags.AddTag(fireTag);
        tags.RemoveTag(fireTag);

        // 组件仍存在于 Entity 上
        Assert.True(entity.HasComponent<GameplayTags>());
    }

    [Fact]
    public void Matches_ParentTag_WhenEntityHasChildTag()
    {
        var world = CreateWorld();
        var entity = world.Store.CreateEntity();
        entity.AddComponent(new GameplayTags());

        var doTTag = GameplayTag.Request("Damage.Fire.DoT");
        ref var tags = ref entity.GetComponent<GameplayTags>();
        tags.AddTag(doTTag);

        // 层级匹配：DoT 是 Damage 的子孙
        var damageTag = GameplayTag.Request("Damage");
        Assert.True(tags.Matches(damageTag));

        // 层级匹配：DoT 是 Damage.Fire 的子孙
        var fireTag = GameplayTag.Request("Damage.Fire");
        Assert.True(tags.Matches(fireTag));
    }

    [Fact]
    public void Matches_ReturnsFalse_WhenNoMatch()
    {
        var world = CreateWorld();
        var entity = world.Store.CreateEntity();
        entity.AddComponent(new GameplayTags());

        var regenTag = GameplayTag.Request("Buff.Regeneration");
        ref var tags = ref entity.GetComponent<GameplayTags>();
        tags.AddTag(regenTag);

        // Buff.Regeneration 与 Damage 不相关
        var damageTag = GameplayTag.Request("Damage");
        Assert.False(tags.Matches(damageTag));
    }

    [Fact]
    public void HasTag_ExactMatch_DoesNotMatchParent()
    {
        var world = CreateWorld();
        var entity = world.Store.CreateEntity();
        entity.AddComponent(new GameplayTags());

        var doTTag = GameplayTag.Request("Damage.Fire.DoT");
        ref var tags = ref entity.GetComponent<GameplayTags>();
        tags.AddTag(doTTag);

        // 精确匹配：Entity 没有 Damage 这个 Tag（只有 DoT）
        var damageTag = GameplayTag.Request("Damage");
        Assert.False(tags.HasTag(damageTag));
    }

    [Fact]
    public void MatchesAny_ReturnsTrue_WhenAnyOverlap()
    {
        var world = CreateWorld();
        var entity1 = world.Store.CreateEntity();
        entity1.AddComponent(new GameplayTags());
        ref var tags1 = ref entity1.GetComponent<GameplayTags>();
        tags1.AddTag(GameplayTag.Request("Damage.Fire"));
        tags1.AddTag(GameplayTag.Request("Buff.Regeneration"));

        var entity2 = world.Store.CreateEntity();
        entity2.AddComponent(new GameplayTags());
        ref var tags2 = ref entity2.GetComponent<GameplayTags>();
        tags2.AddTag(GameplayTag.Request("Damage.Ice"));

        // tags1 和 tags2 共享 Damage 祖先 → 都有 Damage.* 子标签
        Assert.True(tags1.MatchesAny(tags2));
    }

    [Fact]
    public void Query_EntityWithGameplayTags_IncludedInQueryResult()
    {
        var world = CreateWorld();
        var entity = world.Store.CreateEntity();
        entity.AddComponent(new GameplayTags());
        entity.AddComponent(new HealthComponent { Value = 100f });

        ref var tags = ref entity.GetComponent<GameplayTags>();
        tags.AddTag(GameplayTag.Request("StatusEffect.Stunned"));

        var query = world.Store.Query<GameplayTags, HealthComponent>();
        int count = 0;
        foreach (var (gameplayTags, health) in query.Entities)
        {
            count++;
            Assert.True(gameplayTags.Matches(GameplayTag.Request("StatusEffect.Stunned")));
            Assert.Equal(100f, health.Value);
        }
        Assert.Equal(1, count);
    }
}
```

- [ ] **Step 2: 运行测试确认失败**

```bash
dotnet test tests/Gameplay.Tests/Gameplay.Tests.csproj --filter "FullyQualifiedName~GameplayTagsTests"
```
预期：编译失败，`GameplayTags` 类型不存在。

- [ ] **Step 3: 实现 GameplayTags Component**

```csharp
// src/Gameplay/GameplayTags/GameplayTags.cs
using Friflo.Engine.ECS;

namespace Gameplay;

/// <summary>附加到 Entity 的 GameplayTag 运行时集合。</summary>
public struct GameplayTags : IComponent
{
    internal GameplayTagSet tagSet;

    public int Count => tagSet.Count;

    public void AddTag(GameplayTag tag)    => tagSet.Set(tag.id);
    public void RemoveTag(GameplayTag tag) => tagSet.Clear(tag.id);

    public bool HasTag(GameplayTag tag)    => tagSet.Has(tag.id);

    public bool Matches(GameplayTag tag)
        => tagSet.HasAny(GameplayTagManager.GetExpandedSet(tag.id));

    public bool MatchesAny(GameplayTags other)
        => tagSet.HasAny(other.tagSet);
}
```

- [ ] **Step 4: 运行测试确认通过**

```bash
dotnet test tests/Gameplay.Tests/Gameplay.Tests.csproj --filter "FullyQualifiedName~GameplayTagsTests"
```
预期：全部 PASS（8 个测试）。

- [ ] **Step 5: 提交**

```bash
git add src/Gameplay/GameplayTags/GameplayTags.cs tests/Gameplay.Tests/GameplayTags/GameplayTagsTests.cs
git commit -m "feat: GameplayTags ECS Component"
```

---

### Task 5: 边界场景 & 全量回归

**Files:**
- Test: `tests/Gameplay.Tests/GameplayTags/GameplayTagEdgeCaseTests.cs`

**Interfaces:**
- Consumes: all types from Tasks 1–4

- [ ] **Step 1: 写边界测试**

```csharp
// tests/Gameplay.Tests/GameplayTags/GameplayTagEdgeCaseTests.cs
using Xunit;

namespace Gameplay.Tests;

public class GameplayTagEdgeCaseTests
{
    [Fact]
    public void TagName_Trimmed_BeforeRegistration()
    {
        GameplayTagManager.RegisterTags("  Damage  ");
        var tag = GameplayTag.Request("Damage");
        Assert.True(tag.IsValid);
    }

    [Fact]
    public void TagName_Empty_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            GameplayTagManager.RegisterTags(""));
    }

    [Fact]
    public void TagName_StartsWithDot_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            GameplayTagManager.RegisterTags(".Damage"));
    }

    [Fact]
    public void TagName_EndsWithDot_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            GameplayTagManager.RegisterTags("Damage."));
    }

    [Fact]
    public void TagName_DoubleDot_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            GameplayTagManager.RegisterTags("Damage..Fire"));
    }

    [Fact]
    public void TagName_CaseSensitive()
    {
        GameplayTagManager.RegisterTags("Damage");
        var upper = GameplayTag.Request("Damage");
        var lower = GameplayTag.Request("damage");

        Assert.True(upper.IsValid);
        Assert.False(lower.IsValid); // 大小写敏感
    }

    [Fact]
    public void ToString_ReturnsFullName()
    {
        GameplayTagManager.RegisterTags("Damage.Fire");
        var fire = GameplayTag.Request("Damage.Fire");
        Assert.Equal("Damage.Fire", fire.ToString());
    }

    [Fact]
    public void OperateOnMultipleEntities_EachHasOwnTagSet()
    {
        GameplayTagManager.RegisterTags("Damage", "Buff");
        var world = new World(NetMode.Standalone);

        var entityA = world.Store.CreateEntity();
        var entityB = world.Store.CreateEntity();

        entityA.AddComponent(new GameplayTags());
        entityB.AddComponent(new GameplayTags());

        ref var tagsA = ref entityA.GetComponent<GameplayTags>();
        ref var tagsB = ref entityB.GetComponent<GameplayTags>();

        tagsA.AddTag(GameplayTag.Request("Damage"));
        tagsB.AddTag(GameplayTag.Request("Buff"));

        Assert.True(tagsA.HasTag(GameplayTag.Request("Damage")));
        Assert.False(tagsA.HasTag(GameplayTag.Request("Buff")));
        Assert.False(tagsB.HasTag(GameplayTag.Request("Damage")));
        Assert.True(tagsB.HasTag(GameplayTag.Request("Buff")));
    }

    [Fact]
    public void RegisterTags_Incremental_Works()
    {
        // 先注册一批
        GameplayTagManager.RegisterTags("Damage");
        var dmg1 = GameplayTag.Request("Damage");
        Assert.True(dmg1.IsValid);

        // 再注册新的一批（增量）
        GameplayTagManager.RegisterTags("Damage.Fire", "Damage.Ice");
        var fire = GameplayTag.Request("Damage.Fire");
        var ice  = GameplayTag.Request("Damage.Ice");

        Assert.True(fire.IsValid);
        Assert.True(ice.IsValid);
        Assert.True(fire.Matches(dmg1));
        Assert.True(ice.Matches(dmg1));
    }
}
```

- [ ] **Step 2: 运行边界测试**

```bash
dotnet test tests/Gameplay.Tests/Gameplay.Tests.csproj --filter "FullyQualifiedName~GameplayTagEdgeCaseTests"
```
预期：全部 PASS（9 个测试）。

- [ ] **Step 3: 全量回归**

```bash
dotnet test tests/Gameplay.Tests/Gameplay.Tests.csproj
```
预期：全部 PASS（原有 HealthComponentTests + 9 GameplayTagSet + 10 GameplayTagManager + 8 GameplayTags + 9 Edge = ~37 个测试）。

- [ ] **Step 4: 提交**

```bash
git add tests/Gameplay.Tests/GameplayTags/GameplayTagEdgeCaseTests.cs
git commit -m "test: GameplayTag 边界场景测试"
```

---

### Task 6: 双 TFM 编译验证

**Files:**
- 无新建文件

- [ ] **Step 1: 编译 netstandard2.1**

```bash
dotnet build src/Gameplay/Gameplay.csproj -f netstandard2.1
```
预期：BUILD SUCCESS。

- [ ] **Step 2: 编译 net10.0**

```bash
dotnet build src/Gameplay/Gameplay.csproj -f net10.0
```
预期：BUILD SUCCESS。

- [ ] **Step 3: 运行测试（net10.0）**

```bash
dotnet test tests/Gameplay.Tests/Gameplay.Tests.csproj -f net10.0
```
预期：全部 PASS。

- [ ] **Step 4: 提交**

```bash
git add src/Gameplay/Gameplay.csproj
git commit -m "build: 验证双 TFM 编译和测试通过"
```
(如 csproj 无变更，跳过 git add)

---

## 完成标准

- [ ] 所有测试通过（~37 个）
- [ ] `dotnet build` 两个 TFM 全部通过
- [ ] 文件范围命名空间用于所有文件
- [ ] 注释使用中文
- [ ] `src/Gameplay/GameplayTags/` 下 5 个文件（无 GameplayTagQuery.cs）
- [ ] `tests/Gameplay.Tests/GameplayTags/` 下 4 个测试文件
