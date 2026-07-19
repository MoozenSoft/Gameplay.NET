# GameplayTagManager 使用 static 全局注册中心

`GameplayTagManager` 是 `static class`，所有 GameplayTag 注册为全局唯一，不挂在 `World` 实例下。

**Considered Options**

- **static 全局**：与 UE5 `FGameplayTagManager` 设计一致，Tag 是全局唯一的命名空间。采纳。
- **实例化（挂在 World 下）**：每个 `World` 拥有独立的 Tag 集合。适用于多 World 场景，但本项目明确 1 进程 = 1 World，多 World 隔离无需求。被否决。

**Consequences**

- 测试里不需要为每个 World 重新注册 Tag。
- 运行时不需要线程安全——所有 Tag 在启动阶段注册完毕，后续只读。
