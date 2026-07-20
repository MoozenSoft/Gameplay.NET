# GameplayTask 设计文档

日期：2026-07-20

## 目标

实现 GAS 阶段二——GameplayTask（异步任务系统）第一版。仅实现 `DelayTask`（延时等待），奠定 Task 即 Entity + System 驱动的架构基础。

后续版本扩展 `WaitEvent`、`WaitInput` 等类型，并由 `AbilityInstance` Entity 管理 Task 链关系。

## 1. 整体架构

```
src/Gameplay/GameplayTasks/
├── TaskOwnerComponent.cs    # struct : IComponent，Task 拥有者引用
├── TaskStateComponent.cs    # struct : IComponent，Task 状态（Pending/Running/Done/Cancelled）
├── DelayTaskComponent.cs    # struct : IComponent，延时等待数据
├── DelayTaskSystem.cs       # class : QuerySystem，每帧推进 DelayTask
```

### 设计原则

- **Task 即 Entity + Component（纯数据），不由 class + Update() 驱动。** 每个 Task 是一个 Entity，挂上描述它的 Component，由 `TaskSystem` 统一遍历推进。
- **不做 Task 间链接。** 父子/兄弟/后继关系留给后续 `AbilityInstance` 层处理。v1 只做单个 Task 的生命周期。
- **System 不销毁 Task。** 只标记 `Done`/`Cancelled`，销毁由外部（未来的 AbilityInstance）决策。

## 2. TaskOwnerComponent

文件：`src/Gameplay/GameplayTasks/TaskOwnerComponent.cs`

```csharp
using Friflo.Engine.ECS;

namespace Gameplay;

/// <summary>Task 拥有者引用。</summary>
public struct TaskOwnerComponent : IComponent
{
    public Entity Owner;
}
```

## 3. TaskStateComponent

文件：`src/Gameplay/GameplayTasks/TaskStateComponent.cs`

```csharp
using Friflo.Engine.ECS;

namespace Gameplay;

public enum TaskState
{
    Pending,
    Running,
    Done,
    Cancelled,
}

/// <summary>Task 的运行状态。</summary>
public struct TaskStateComponent : IComponent
{
    public TaskState State;
}
```

## 4. DelayTaskComponent

文件：`src/Gameplay/GameplayTasks/DelayTaskComponent.cs`

```csharp
using Friflo.Engine.ECS;

namespace Gameplay;

/// <summary>延时等待——累积 Elapsed 到达 Duration 后 Done。</summary>
public struct DelayTaskComponent : IComponent
{
    public float Duration;
    public float Elapsed;
}
```

## 5. DelayTaskSystem

文件：`src/Gameplay/GameplayTasks/DelayTaskSystem.cs`

```csharp
using Friflo.Engine.ECS;

namespace Gameplay;

/// <summary>
/// 每帧推进 DelayTask。<br/>
/// Pending → Running → (Elapsed >= Duration → Done)。
/// 不处理 Done/Cancelled 的销毁，由外部决策。
/// </summary>
public class DelayTaskSystem : QuerySystem<TaskStateComponent, DelayTaskComponent>
{
    protected override void OnUpdate()
    {
        Query.ForEachEntity(
            (ref TaskStateComponent state, ref DelayTaskComponent delay, Entity entity) =>
        {
            switch (state.State)
            {
                case TaskState.Pending:
                    state.State = TaskState.Running;
                    break;

                case TaskState.Running:
                    delay.Elapsed += Tick.deltaTime;
                    if (delay.Elapsed >= delay.Duration)
                        state.State = TaskState.Done;
                    break;

                // Done / Cancelled → 不处理，等外部决策
            }
        });
    }
}
```

### SystemRoot 注册示例

```csharp
var root = new SystemRoot(store) {
    new DelayTaskSystem(),
};
root.Update(new UpdateTick(deltaTime, 0));
```

## 6. 生命周期

```
外部创建 Task Entity:
  entity.AddComponent(new TaskOwnerComponent { Owner = sourceEntity });
  entity.AddComponent(new TaskStateComponent { State = TaskState.Pending });
  entity.AddComponent(new DelayTaskComponent { Duration = 3f });

帧 N:   System 检测 Pending  → 标记 Running
帧 N+1: System 检测 Running → Elapsed += deltaTime
帧 N+2: System 检测 Running → Elapsed += deltaTime
...
帧 M:   Elapsed >= Duration → 标记 Done

外部观察:
  ref var state = ref entity.GetComponent<TaskStateComponent>();
  if (state.State == TaskState.Done) { entity.DeleteEntity(); }
```

### 设计决策

- **每种 Task 类型一个独立 System**：`DelayTaskSystem` query `DelayTaskComponent`，未来 `WaitEventTaskSystem` query `WaitEventTaskComponent`。Friflo Query 按 Component 签名匹配，不重叠。
- **不设 `TaskType` 枚举**：YAGNI。Task 的 Component 签名本身就定义了类型。
- **Pending → Running 和第一次累加分两帧**：`switch` 在同一帧只走一个分支，不会在变为 Running 后立即累加 Elapsed。
- **Duration=0 合法**：下一帧直接 Done，不做运行时校验。
- **`TaskOwner` 保留为数据占位**：v1 无消费者，留给 AbilityInstance 使用。

## 7. 测试计划

文件：`tests/Gameplay.Tests/GameplayTasks/GameplayTaskTests.cs`

每个测试独立创建 World + SystemRoot，不共享状态。

| 测试 | 说明 |
|------|------|
| `PendingTask_TransitionsToRunning_OnUpdate` | Pending 的 Task 在第一次 Tick 后变为 Running |
| `RunningTask_IncrementsElapsed_OnUpdate` | Running 状态下，每次 Tick 累加 Elapsed |
| `RunningTask_TransitionsToDone_WhenElapsedExceedsDuration` | Elapsed >= Duration 时变为 Done |
| `DoneTask_StaysDone_OnUpdate` | Done 状态不会被 System 改变 |
| `CancelledTask_StaysCancelled_OnUpdate` | Cancelled 状态不会被 System 改变 |
| `DoneTask_IsNotAutoDeleted` | Done 后 Entity 仍然存在，TestSystem 不自动销毁 |

## 8. 不在范围内

- `WaitEventTask`、`WaitInputTask` 等类型
- AbilityInstance Entity（提供者、PredictionKey、共享变量等）
- Task 间链接（ParentTask / NextTask / ChildTask）
- Task 自动销毁
