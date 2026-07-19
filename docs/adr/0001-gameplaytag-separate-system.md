# 建独立 GameplayTag 系统，而非使用 Friflo 内置 ITag

Friflo ECS 提供了编译时的 `ITag` 接口用于给 Entity 打标签，但 Gameplay.NET 需要独立的 GameplayTag 系统。主要原因是 GAS 对层级匹配是 Must-have 需求——`Damage.Fire` 必须被 `Matches(Damage)` 匹配到——而 `ITag` 是扁平的，不支持层级关系。

**Considered Options**

- **使用 Friflo ITag**：每个 Tag 声明为一个 struct 实现 `ITag`。无层级、上限 255 个、添加/移除会改变 Archetype。被否决——层级匹配是 GAS 核心需求。
- **自建运行时 GameplayTag 系统**：`IComponent` 存位集，不改变 Archetype，支持几万个 Tag，支持层级匹配。采纳。

**Consequences**

- GameplayTag 的值存在 Component 内部数据中，无法用 Friflo 的查询引擎（结构级筛选）直接按 Tag 值过滤。需要在查询循环内用位集做第二次筛选，但这部分 O(1) 性能足够。
