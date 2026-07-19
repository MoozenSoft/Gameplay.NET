# RegisterTags 与 RequestTag 的职责分离

`RegisterTags` 是唯一创建 GameplayTag 的入口，具备自动补齐父节点的副作用。`RequestTag` 是纯只读的查询方法，不存在则返回 `GameplayTag.Invalid`。

**Considered Options**

- **统一入口 RequestTag（创建 + 查询）**：单次调用如果 Tag 不存在就创建。方便但危险——打字错误如 `"Damge.Fire"` 会静默注册错误 Tag。被否决。
- **职责分离**：`RegisterTags` 负责创建（启动阶段调用，自动补齐父节点），`RequestTag` 负责查询（运行时调用，只读）。采纳。

**Consequences**

- 运行时没有意外创建 Tag 的风险，所有 Tag 需在 `RegisterTags` 中显式声明。
- Dirty + `Build()` 模式：`RegisterTags` 标记脏标记，下次 `RequestTag` 自动触发展开集重算。
- `RegisterTags` 可被多次调用以增量注册。
