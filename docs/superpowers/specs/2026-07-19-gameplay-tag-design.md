# GameplayTag 设计文档

日期：2026-07-19

## 目标

实现 ECS 版 GAS 的第一阶段——GameplayTag（层级标签系统）。参考 UE5 FGameplayTag，提供层级命名、层级匹配和运行时注册能力，以 Friflo ECS Component 的形式集成到 Entity 上。

代码放在 `src/Gameplay/GameplayTags/` 目录。

## 1. 整体架构

```
src/Gameplay/GameplayTags/
├── GameplayTag.cs           # struct，轻量级 Tag 句柄（包装 int id）
├── GameplayTagNode.cs       # internal，层级树节点
├── GameplayTagManager.cs    # static class，全局注册中心 + 展开集预计算
├── GameplayTagSet.cs        # internal struct，可扩展 long[] 位集
└── GameplayTags.cs          # struct : IComponent，Entity 上的 Tag 运行时集合

tests/Gameplay.Tests/GameplayTags/
└── GameplayTagTests.cs
```

### 与 Friflo ECS 查询引擎的关系

Friflo 的 `QueryFilter` 只支持结构级筛选（按 Component 类型判断有无），不支持按 Component 内部数据筛选。GameplayTag 的值存在 `GameplayTags` Component 的位集**内部**，因此无法在 Friflo 查询引擎层面做数据级筛选。

**两层筛选方案**：

```
第一层（Friflo 查询引擎）：
  AllComponents<GameplayTags>()  →  排除所有没挂 GameplayTags 组件的 Entity
                                    在 Archetype 级别跳过，非目标 Entity 的访问开销为 0

第二层（我们的位集）：
  tags.Matches(tag)              →  在已筛选的 Entity 循环里，用 long[] 位集做 O(1) 成员检查
                                    每个检查只需几次位运算（&、移位）
```

## 2. GameplayTagManager（全局注册中心）

文件：`src/Gameplay/GameplayTags/GameplayTagManager.cs`

```csharp
namespace Gameplay;

public static class GameplayTagManager
{
    // 字符串 → Tag ID
    private static Dictionary<string, int> nameToId;

    // Tag ID → 节点信息（索引即 id，id=0 保留为无效）
    private static GameplayTagNode[] nodes;

    // Tag ID → 展开子孙位集（预计算，查询时直接用）
    private static GameplayTagSet[] expandedSets;

    /// <summary>
    /// 批量注册（通常在游戏启动时调用一次）。<br/>
    /// 这是唯一创建 GameplayTag 的入口。子 Tag 注册时自动创建缺失的父节点。
    /// </summary>
    public static void RegisterTags(params string[] tagNames);

    /// <summary>
    /// 获取已注册的 GameplayTag。只读，不创建。不存在则返回 <see cref="GameplayTag.Invalid"/>。
    /// </summary>
    public static GameplayTag RequestTag(string tagName);

    // 内部
    private static bool dirty;  // 树变更后置脏，Build() 时重算展开集
    internal static void Build();   // 统一重算所有展开集（惰性触发：首次 RequestTag 时）

    // 内部查询
    internal static string GetName(int id);
    internal static ReadOnlySpan<long> GetExpandedSet(int id);
    internal static bool Matches(int childId, int parentId);
}
```

**设计决策**：

- **线程安全**：不做。`RegisterTags` 在启动阶段调用一次，后续所有操作只读。与 UE5 FGameplayTag 一致。
- **数组而非 List**：`nodes[id]` 和 `expandedSets[id]` 用数组下标 O(1) 访问，无 boxing。
- **预计算展开集**：采用 Dirty + `Build()` 模式。`RegisterTags` 标记脏，首次 `RequestTag`（或手动 `Build()`）统一重算所有展开集。
- **Tag 名验证**：去首尾空格，不允许空字符串，不允许以点开头/结尾，不允许连续点（`..`）。大小写敏感。除此之外不限制字符集。
- **重复注册**：幂等，已存在的 Tag 直接返回已有句柄。

### 展开集示例

```
注册顺序：Damage, Damage.Fire, Damage.Fire.DoT, Damage.Ice

Damage            → id=1, 展开集={1,2,3,4}  (自身 + 所有子孙)
Damage.Fire       → id=2, 展开集={2,3}      (自身 + DoT)
Damage.Fire.DoT   → id=3, 展开集={3}         (自身，无子孙)
Damage.Ice        → id=4, 展开集={4}         (自身，无子孙)
```

## 3. GameplayTag（句柄）

文件：`src/Gameplay/GameplayTags/GameplayTag.cs`

```csharp
namespace Gameplay;

/// <summary>不可变的轻量 GameplayTag 句柄，包装一个 int id。</summary>
public readonly struct GameplayTag : IEquatable<GameplayTag>
{
    internal readonly int id;

    public bool IsValid => id > 0;

    /// <summary>从字符串获取或注册 Tag。</summary>
    public static GameplayTag Request(string tagName)
        => GameplayTagManager.RequestTag(tagName);

    /// <summary>层级匹配：此 Tag 是否是 parent 自身或子孙。</summary>
    public bool Matches(GameplayTag parent)
        => GameplayTagManager.Matches(id, parent.id);

    /// <summary>精确匹配（仅检查 id 相等）。</summary>
    public bool MatchesExact(GameplayTag other) => id == other.id;

    public bool Equals(GameplayTag other) => id == other.id;
    public override bool Equals(object obj) => obj is GameplayTag other && Equals(other);
    public override int GetHashCode() => id;
    public override string ToString() => GameplayTagManager.GetName(id) ?? "Invalid";
}
```

**设计决策**：

- **值类型 struct**：零 GC 压力。32 字节（仅一个 int + 对齐），传递开销约等于传 int。
- **不存字符串引用**：名称通过 `GetName(id)` 从 Manager 查询。句柄本身极轻。
- **构造器为 `internal`**：`GameplayTag` 实例只能通过 `GameplayTagManager.RequestTag()` 获取，确保全局唯一性和一致性。

## 4. GameplayTagNode（层级树节点）

文件：`src/Gameplay/GameplayTags/GameplayTagNode.cs`

```csharp
namespace Gameplay;

internal sealed class GameplayTagNode
{
    internal readonly string Name;       // 短名（如 "Fire"）
    internal readonly string FullName;   // 全名（如 "Damage.Fire"）
    internal readonly int Id;
    internal GameplayTagNode Parent;
    internal readonly List<GameplayTagNode> Children;
}
```

不暴露给 Consumer，仅供 `GameplayTagManager` 内部维护层级树。

## 5. GameplayTagSet（可扩展位集）

文件：`src/Gameplay/GameplayTags/GameplayTagSet.cs`

```csharp
namespace Gameplay;

/// <summary>内部可扩展位集，用 long[] 存储任意数量的 Tag 位。</summary>
internal struct GameplayTagSet
{
    private long[] bits;       // null = 空集（延迟分配）
    private int    count;      // 已设置的 bit 数量

    public int Count => count;

    /// <summary>置位。</summary>
    public void Set(int id);

    /// <summary>清除。</summary>
    public void Clear(int id);

    /// <summary>暴露内部位数组，供展开集查询。</summary>
    internal ReadOnlySpan<long> Bits => bits;

    /// <summary>检查是否包含指定 id。</summary>
    public bool Has(int id);

    /// <summary>检查是否与另一个 GameplayTagSet 有交集。</summary>
    public bool HasAny(in GameplayTagSet other)
        => HasAny(other.Bits);

    /// <summary>检查是否与展开集有交集（层级查询核心路径）。</summary>
    public bool HasAny(ReadOnlySpan<long> expandedSet);

    /// <summary>检查是否包含另一个位集的全部位。</summary>
    public bool HasAll(in GameplayTagSet other);
}
```

**设计决策**：

- **`long[]` 而非 `BitArray` 或 `List<long>`**：`long[]` 可以做批量位运算（`& | ^ ~`），SIMD 友好。每 64 个 Tag 多 8 字节。100 个 Tag → 2 个 long = 16 字节。
- **延迟分配 `bits`**：空 Entity 没有 Tag 时 `bits` 为 null，`Has()` 直接返回 false。首次 `Set()` 时才分配。
- **扩容策略**：按需精确扩容到 `id >> 6 + 1`（刚好容下），不翻倍。因为 Tag 总数在注册时就已知。
- **`HasAny(ReadOnlySpan<long>)`**：展开集以 `ReadOnlySpan<long>` 传入，零分配。核心热点路径极短。

### 层级查询原理

```
Entity 的 tagSet:     bits=[0, 0, 1, 1]     ← 有 id=2(Damage.Fire), id=3(Damage.Ice)
                                       ↑ ↑
                                      2 3

Damage(id=1) 展开集:   expanded=[0, 1, 1, 1]  ← {1, 2, 3, 4}
                                     ↑ ↑ ↑ ↑
                                     1 2 3 4

HasAny: [0,0,1,1] & [0,1,1,1] = [0,0,1,1] ≠ 0 → 命中！
              ↑↑     ↑↑↑          ↑↑
```

## 6. GameplayTags Component

文件：`src/Gameplay/GameplayTags/GameplayTags.cs`

```csharp
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

    public bool MatchesAny(GameplayTags other) => tagSet.HasAny(other.tagSet);
}
```

**设计决策**：

- **作为 IComponent 而非 Friflo ITag**：突破 255 上限，支持层级，不改变 Archetype（添加/移除 GameplayTag 不引起结构性变更）。
- **内部字段名 `tagSet`**（非 `set`）：与 C# 关键字区分，避免混淆。

## 7. 使用示例

```csharp
// 1. 启动时一次性注册所有 GameplayTag
GameplayTagManager.RegisterTags(
    "Damage",
    "Damage.Fire",
    "Damage.Fire.DoT",
    "Damage.Ice",
    "StatusEffect.Stunned",
    "StatusEffect.Rooted"
);

// 2. 获取句柄（轻量 struct，可存 static readonly field）
private static readonly GameplayTag DamageTag = GameplayTag.Request("Damage");
private static readonly GameplayTag StunTag  = GameplayTag.Request("StatusEffect.Stunned");

// 3. 给 Entity 添加 Tag
var entity = world.Store.CreateEntity();
entity.AddComponent(new GameplayTags());
entity.GetComponent<GameplayTags>().AddTag(DamageTag);
entity.GetComponent<GameplayTags>().AddTag(StunTag);

// 4. System 中查询
var query = world.Store.Query<GameplayTags, HealthComponent>();

foreach (var (tags, hp) in query.Entities)
{
    if (tags.Matches(DamageTag))   // 层级匹配：Damage.Fire.DoT 也命中
    {
        hp.Value -= 10;
    }

    if (tags.HasTag(StunTag))      // 精确匹配
    {
        // 眩晕中，跳过行动
    }
}
```

## 8. 测试计划

文件：`tests/Gameplay.Tests/GameplayTags/GameplayTagTests.cs`

| 测试 | 说明 |
|------|------|
| `RegisterTags_CreatesHierarchy` | 层级注册，验证父子关系 |
| `RequestTag_ReturnsSameInstance` | 同一个 Tag 名返回相同 id 的句柄 |
| `Matches_ParentMatchesChild` | `Damage.Fire.Matches(Damage)` → true |
| `Matches_ChildDoesNotMatchParent` | `Damage.Matches(Damage.Fire)` → false |
| `MatchesExact_SameIdReturnsTrue` | 精确匹配验证 |
| `GameplayTags_AddAndHasTag` | Entity 添加 Tag 后可查询 |
| `GameplayTags_RemoveTag` | 移除 Tag 后 HasTag 返回 false |
| `GameplayTags_MatchesParent` | Entity 有 `Damage.Fire`，`Matches(Damage)` → true |
| `GameplayTags_MatchesAny` | 两个 Entity 的 GameplayTags 交集检查 |
| `GameplayTagSet_HasAny` | 位集 HasAny 的逻辑正确性 |
| `GameplayTagSet_LargeId` | id > 64 的扩容和位操作正确性 |

## 9. 不在范围内

- Tag Change Events（Tag 增删时的回调）
- JSON 序列化支持
- `[GameplayTag("...")]` 代码生成 Attribute
- 线程安全（运行时只读，启动时一次性注册）
