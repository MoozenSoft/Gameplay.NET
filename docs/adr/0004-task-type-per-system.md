# 每种 Task 类型一个独立 System

GameplayTask 的每种异步类型（Delay、WaitEvent、WaitInput 等）对应一个独立的 `QuerySystem`，而不是用一个万能 `TaskSystem` 做 switch 分发。

**Considered Options**

- **统一 TaskSystem + switch(ComponentType)**：所有 Task 类型共享一个 System，内部用 switch 或类型标记分发。打破了 ECS 的 System 职责单一原则，新增 Task 类型需要修改已有 System。被否决。
- **每种 Task 类型一个独立 System**：`DelayTaskSystem` query `DelayTaskComponent`，`WaitEventSystem` query `WaitEventTaskComponent`。Friflo 的 Query 按 Component 签名匹配，天然不重叠。新增 Task 类型只需加新 Component + 新 System，不碰已有代码。采纳。

**Consequences**

- v1 中 `DelayTaskSystem` 命名带类型前缀，后续加新 Task 时命名模式一致。
- 各类型 Task 的公共阶段（如 Pending→Running）可能有重复代码。如需复用，可在公共 `TaskStateComponent` 层提取，但目前 YAGNI。
