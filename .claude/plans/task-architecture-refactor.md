# 计划：Task 架构重构 —— Runtime + Driver + Task Data

## Context

当前 GameplayTask/AbilityTask 虽然已是组件化的，但命名和组织仍带着 UE 继承关系的痕迹：
- Component 以 `Wait*` / `*Task*` 命名（API 视角而非能力视角）
- `AbilityTaskContextComponent` 把 Task 强行绑定到 Ability
- `AbilityTaskSystem` 只服务 Ability 生命周期
- 概念上仍区分 GameplayTask 和 AbilityTask

目标：将 Task 重构为 **Runtime（生命周期）+ Component（数据能力）+ System（Driver）** 三层架构。GameplayTask/AbilityTask 退化为 API 层的 Facade/Factory。

**所有 Component 和 System 统一放在 `Gameplay.Tasks` 下**——Component 代表能力，不因引用某个命名空间的类型就该留在那个命名空间。

## 目标目录结构

```
Gameplay.Tasks/
│
├── Runtime/
│   ├── TaskStateComponent.cs
│   ├── TaskOwnerComponent.cs
│   ├── TaskSchedulerSystem.cs
│   ├── TaskCommands.cs
│   └── ITaskCompletionListener.cs
│
├── Components/
│   ├── DelayComponent.cs
│   ├── GameplayEventListener.cs
│   ├── TagListenerComponent.cs
│   ├── AttributeListener.cs
│   └── CommitPhaseListener.cs
│
├── Systems/
│   ├── DelaySystem.cs
│   ├── GameplayEventSystem.cs
│   ├── TagListenerSystem.cs
│   ├── AttributeListenerSystem.cs
│   └── CommitPhaseListenerSystem.cs
│
└── Builders/
    └── Tasks.cs
```

## 已确认的设计决策（grilling 结论）

| # | 决策 | 结论 |
|---|------|------|
| Q1 | AllTasksDone 检测机制 | **子 Entity 遍历**（复用现有 `AllTasksDone` 逻辑，不引入 Owner 计数） |
| Q2 | 完成通知机制 | **接口抽象 `ITaskCompletionListener`**（Tasks 域定义，Abilities 域实现并注册） |
| Q3 | Pending→Running 归属 | **各 Driver 保留**（Pending 阶段含能力专属初始化：注册监听、快照等，Scheduler 统一会耦合） |
| Q4 | 销毁执行者与时序 | **Scheduler 统一销毁**（扫到 Done/Cancelled → 通知 Owner（当帧 Task 仍活可读数据）→ 入队 → 帧末 ProcessPendingDeletions） |
| Q5 | 取消语义 | **所有子 Task 显式设 Cancelled**（生命周期完整，为未来延迟删除窗口留入口） |
| Q6 | CommitPhaseListener 循环依赖 | **接受**（单一程序集内合法；标注技术债，拆程序集时迁回 Abilities） |

## 改动概览

### 核心变化
1. **Component 按能力命名**：`DelayComponent` 而非 `DelayTaskComponent`/`WaitDelayComponent`
2. **合并同类 Component**：`WaitGameplayTagAddedComponent` + `WaitGameplayTagRemovedComponent` → `TagListenerComponent`（内含 `TagCondition` 枚举）
3. **用 `TaskOwnerComponent` 替代 `AbilityTaskContextComponent`**：去 Ability 绑定
4. **`AbilityTaskSystem` → `TaskSchedulerSystem`**：Generic 生命周期管理，通过 `ITaskCompletionListener` 通知 Owner
5. **新增 Builders 层**：`Tasks.cs`（Builder/Factory）

### 文件变更清单

#### Gameplay.Tasks/Runtime/（核心基础设施）

| 操作 | 文件 | 说明 |
|------|------|------|
| 保留 | `TaskStateComponent.cs` | 无变化 |
| 重写 | `TaskOwnerComponent.cs` | `Owner`（谁创建的 Task）+ `TaskHandle`。**不加 ParentEntity 字段**——层次关系由 Friflo `AddChild`/`ChildEntities` 提供（`Entity.Parent` 已存在） |
| **新增** | `TaskSchedulerSystem.cs` | 生命周期管理（替代 AbilityTaskSystem） |
| **新增** | `TaskCommands.cs` | `Complete()`, `Cancel()`, `Destroy()` 纯状态命令 |
| **新增** | `ITaskCompletionListener.cs` | `OnAllTasksDone(Entity owner)` 通知接口（Tasks 域定义，Abilities 域实现） |

#### Gameplay.Tasks/Components/（数据组件 —— 能力视角）

| 操作 | 来源 | 目标 |
|------|------|------|
| 重命名 | `DelayTaskComponent`（Tasks） | `DelayComponent` |
| 重命名+移动 | `WaitGameplayEventComponent`（Abilities） | `GameplayEventListener` |
| **合并+移动** | `WaitGameplayTagAddedComponent` + `WaitGameplayTagRemovedComponent`（Abilities） | `TagListenerComponent` |
| 重命名+移动 | `WaitAttributeChangeComponent`（Abilities） | `AttributeListener` |
| 重命名+移动 | `WaitAbilityCommitComponent`（Abilities） | `CommitPhaseListener` |
| **删除** | `WaitCancelComponent`（Abilities） | 由 TaskScheduler.Cancel() 统一处理 |
| **删除** | `AbilityTaskContextComponent`（Abilities） | 由 TaskOwnerComponent 替代 |

#### Gameplay.Tasks/Systems/（Driver —— 一种能力一个 System）

| 操作 | 来源 | 目标 |
|------|------|------|
| 重命名 | `DelayTaskSystem`（Tasks） | `DelaySystem` |
| 重命名+移动 | `WaitGameplayEventTaskSystem`（Abilities） | `GameplayEventSystem` |
| 重命名+移动 | `WaitGameplayTagTaskSystem`（Abilities） | `TagListenerSystem` |
| 重命名+移动 | `WaitAttributeChangeTaskSystem`（Abilities） | `AttributeListenerSystem` |
| 重命名+移动 | `WaitAbilityCommitTaskSystem`（Abilities） | `CommitPhaseListenerSystem` |

#### Gameplay.Tasks/Builders/（Builder 层）

| 操作 | 文件 | 说明 |
|------|------|------|
| **新增** | `Tasks.cs` | 统一 API：`Tasks.Delay()`, `Tasks.WaitEvent()`, `Tasks.WaitTag()` 等 |

#### Gameplay.Abilities/AbilityTask/（清理）

| 操作 | 文件 | 说明 |
|------|------|------|
| 删除 | `WaitDelayTask.cs` | 移入 `Builders/Tasks.cs` |
| 删除 | `WaitGameplayEventTask.cs` | Component → Components/, System → Systems/ |
| 删除 | `WaitGameplayTagTask.cs` | 合并到 TagListener |
| 删除 | `WaitAttributeChangeTask.cs` | Component → Components/, System → Systems/ |
| 删除 | `WaitAbilityCommitTask.cs` | Component → Components/, System → Systems/ |
| 删除 | `WaitCancelTask.cs` | 功能由 TaskScheduler 替代 |
| 删除 | `AbilityTaskSystem.cs` | 移至 Runtime/TaskSchedulerSystem |
| 删除 | `AbilityTaskContextComponent.cs` | 由 TaskOwnerComponent 替代 |

#### Gameplay.Abilities/GameplayAbilitiesFeature.cs（注册更新）

- 更新所有 System 引用：`DelayTaskSystem` → `DelaySystem` 等
- 新增 `TaskSchedulerSystem` 注册
- 移除 `AbilityTaskSystem`、`Wait*TaskSystem`，替换为新类型名
- 实现 `ITaskCompletionListener`（或注册适配器）：`OnAllTasksDone(owner)` → `AbilityActivationManager.CancelAbility(owner)`

### 关键设计决策

#### 1. `TaskOwnerComponent` 替代 `AbilityTaskContextComponent`

```csharp
// 当前（绑死 Ability）
public struct AbilityTaskContextComponent : IComponent
{
    public Entity ActiveAbility;
    public int TaskHandle;
}

// 新（通用 Owner 模型）
public struct TaskOwnerComponent : IComponent
{
    public Entity Owner;      // 谁创建了这个 Task（Ability/AI/Quest/任意 Entity）
    public int TaskHandle;    // 保留
}
```

层次关系（`AddChild`/`ChildEntities`）与 Owner 引用并存：**层次关系用于 AllTasksDone 检测**（Q1），Owner 引用用于通知和语义。

#### 2. TagListener 合并 Added + Removed

```csharp
public enum TagCondition { Added, Removed }

public struct TagListenerComponent : IComponent
{
    public GameplayTag Tag;
    public TagCondition Condition;
    public bool WasPresent; // Removed 模式用于检测注册时 Tag 是否存在
}
```

`TagListenerSystem` 在同一个 Query 内 switch `Condition`——同一能力内的分支，不是跨能力的 switch。

#### 3. `TaskSchedulerSystem` 替代 `AbilityTaskSystem`

```csharp
// Query: TaskStateComponent + TaskOwnerComponent
// 职责:
//   1. 检测 Done/Cancelled 的 Task（状态为唯一事实来源）
//   2. 对每个完成的 Task: 子 Entity 遍历检查 Owner 的所有 Task 是否全部完成（AllTasksDone）
//   3. 全部完成 → 通知 ITaskCompletionListener.OnAllTasksDone(owner)（当帧 Task 仍活，可读数据）
//   4. 入队延迟销毁该 Task → 帧末 ProcessPendingDeletions
// 不做的:
//   - Pending→Running 转移（各 Driver 负责，Pending 含能力专属初始化）
//   - 直接依赖 AbilityActivationManager（通过接口解耦）
```

#### 4. 取消语义（Q5）

```csharp
// TaskSchedulerSystem.Cancel(entity):
//   递归遍历所有子 Task → 全部设 Cancelled → 入队延迟销毁
//   Owner 收到 OnAllTasksDone 通知后决定自身行为（Ability: CancelAbility）
// 删除 WaitCancelComponent——取消是通用规则，不再需要标记
```

#### 5. 完成时序（Q4）

```
Phase 1: Driver 推进（Pending→Running + 能力专属初始化）
Phase 2: TaskSchedulerSystem 扫到 Done/Cancelled → 通知 Owner（Task 仍活）→ 入队销毁
Phase 3: (同帧末) ProcessPendingDeletions 删除
```

### 技术债（Q6）

`CommitPhaseListenerSystem` 引用 `ActiveAbilityComponent`/`EAbilityInstanceState`（Abilities 域类型），形成 Tasks → Abilities → Tasks 命名空间循环。**接受**：单一程序集内合法（Gameplay.dll 不依赖其他项目），拆程序集时迁回 Abilities 即可。

### 不影响的部分

- `Gameplay.Abilities/Ability/AbilityActivationManager.cs` —— `CancelAbility` 中 WaitCancel 遍历逻辑需适配为通用递归取消
- Friflo ECS 依赖不变

## 实施步骤

### Step 1：创建新目录结构 + 核心 Runtime
1. 创建 `src/Gameplay/Gameplay.Tasks/Runtime/`、`Components/`、`Systems/`、`Builders/` 子目录
2. 移动 `TaskStateComponent.cs`、`TaskOwnerComponent.cs` 到 `Runtime/`
3. 重写 `TaskOwnerComponent`（`Owner` + `TaskHandle`）
4. 新建 `TaskCommands.cs`、`TaskSchedulerSystem.cs`、`ITaskCompletionListener.cs`

### Step 2：重命名 Core Components + Systems（Tasks 域）
1. `DelayTaskComponent` → `DelayComponent`，移至 `Components/`
2. `DelayTaskSystem` → `DelaySystem`，移至 `Systems/`，更新 Query 为 `TaskState + DelayComponent`

### Step 3：迁移 AbilityTask Components（按能力重命名）
1. `WaitGameplayEventComponent` → `GameplayEventListener` 移至 `Components/`
2. `WaitGameplayTagAddedComponent` + `WaitGameplayTagRemovedComponent` → 合并为 `TagListenerComponent` 移至 `Components/`
3. `WaitAttributeChangeComponent` → `AttributeListener` 移至 `Components/`
4. `WaitAbilityCommitComponent` → `CommitPhaseListener` 移至 `Components/`

### Step 4：迁移 AbilityTask Systems（按能力重命名）
1. `WaitGameplayEventTaskSystem` → `GameplayEventSystem` 移至 `Systems/`
2. `WaitGameplayTagTaskSystem` → `TagListenerSystem` 移至 `Systems/`
3. `WaitAttributeChangeTaskSystem` → `AttributeListenerSystem` 移至 `Systems/`
4. `WaitAbilityCommitTaskSystem` → `CommitPhaseListenerSystem` 移至 `Systems/`
5. 所有 System 更新：用 `TaskOwnerComponent` 替代 `AbilityTaskContextComponent`

### Step 5：新建 TaskSchedulerSystem + 更新 GameplayAbilitiesFeature
1. 编写 `TaskSchedulerSystem`（AllTasksDone 遍历 + 接口通知 + 入队销毁）
2. `GameplayAbilitiesFeature` 实现 `ITaskCompletionListener` 并注册
3. 替换 System 引用为新的类型名

### Step 6：创建 Builders 层
1. 新建 `Builders/Tasks.cs`
2. 迁移 `WaitDelayTask.Create()` 逻辑 → `Tasks.Delay()`
3. 添加 `Tasks.WaitEvent()`、`Tasks.WaitTag()` 等工厂方法

### Step 7：更新测试
1. 更新 `GameplayTaskTests.cs`：`DelayTaskComponent` → `DelayComponent`，`DelayTaskSystem` → `DelaySystem`
2. 更新 `AbilityTaskSystemTests.cs`：`AbilityTaskContextComponent` → `TaskOwnerComponent`，`AbilityTaskSystem` → `TaskSchedulerSystem`
3. 更新 `WaitDelayTaskTests.cs`：适配新 API 和新 Component 名
4. 更新 `WaitGameplayEventTaskTests.cs`：`WaitGameplayEventComponent` → `GameplayEventListener`

### Step 8：清理 + 删除
1. 删除 `Gameplay.Abilities/AbilityTask/` 目录下所有已迁移文件
2. 删除 `WaitCancelTask.cs`（`WaitCancelComponent` 移除，`AbilityActivationManager.CancelAbility` 中适配）

### Step 9：构建 + 运行测试验证

## 验证

```bash
# 构建
dotnet build Gameplay.NET.slnx

# 运行全部测试
dotnet test Gameplay.NET.slnx

# 运行 Task 相关测试
dotnet test tests/Gameplay.Tests/Gameplay.Tests.csproj --filter "FullyQualifiedName~Task"
dotnet test tests/Gameplay.Tests/Gameplay.Tests.csproj --filter "FullyQualifiedName~AbilityTask"
```
