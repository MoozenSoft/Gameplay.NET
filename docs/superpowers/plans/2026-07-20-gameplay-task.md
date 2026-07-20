# GameplayTask 实现计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 实现 GAS GameplayTask 第一版——3 个数据 Component + DelayTaskSystem，奠定 Task 即 Entity + System 驱动的基础

**Architecture:** Task 作为纯数据 Entity（Component 组合）+ 每种 Task 类型独立 System。v1 仅 `DelayTask`：Pending → Running → (Elapsed >= Duration → Done)。System 只标记 Done，不销毁 Entity。

**Tech Stack:** C# LangVersion 12, netstandard2.1 + net10.0, Friflo.Engine.ECS 3.x, xUnit

## Global Constraints

- TargetFrameworks: `netstandard2.1;net10.0`
- LangVersion: 12
- Nullable: enable
- 文件范围命名空间：`namespace Gameplay;`
- 注释和文档使用中文
- TDD：先写测试 → 确认失败 → 实现 → 确认通过 → 提交
- 0 GC 优先（struct Component，System 用 Friflo QuerySystem）

---

### Task 1: 数据 Component（TaskOwner + TaskState）

**Files:**
- Create: `src/Gameplay/GameplayTasks/TaskOwnerComponent.cs`
- Create: `src/Gameplay/GameplayTasks/TaskStateComponent.cs`

**Interfaces:**
- Produces: `public struct TaskOwnerComponent : IComponent` with `Entity Owner`
- Produces: `public enum TaskState { Pending, Running, Done, Cancelled }`
- Produces: `public struct TaskStateComponent : IComponent` with `TaskState State`

- [ ] **Step 1: 创建源码目录和文件**

```bash
mkdir -p src/Gameplay/GameplayTasks
```

```csharp
// src/Gameplay/GameplayTasks/TaskOwnerComponent.cs
using Friflo.Engine.ECS;

namespace Gameplay;

/// <summary>Task 拥有者引用。v1 为数据占位，留待 AbilityInstance 使用。</summary>
public struct TaskOwnerComponent : IComponent
{
    public Entity Owner;
}
```

```csharp
// src/Gameplay/GameplayTasks/TaskStateComponent.cs
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

- [ ] **Step 2: 编译验证**

```bash
dotnet build src/Gameplay/Gameplay.csproj -f net10.0
```
预期：BUILD SUCCESS，0 warnings。

- [ ] **Step 3: 提交**

```bash
git add src/Gameplay/GameplayTasks/TaskOwnerComponent.cs src/Gameplay/GameplayTasks/TaskStateComponent.cs
git commit -m "feat: GameplayTask 数据 Component — TaskOwner + TaskState"
```

---

### Task 2: DelayTaskComponent + DelayTaskSystem（TDD）

**Files:**
- Create: `src/Gameplay/GameplayTasks/DelayTaskComponent.cs`
- Create: `src/Gameplay/GameplayTasks/DelayTaskSystem.cs`
- Test: `tests/Gameplay.Tests/GameplayTasks/GameplayTaskTests.cs`

**Interfaces:**
- Consumes: `TaskStateComponent`, `TaskState`
- Produces: `public struct DelayTaskComponent : IComponent` with `float Duration`, `float Elapsed`
- Produces: `public class DelayTaskSystem : QuerySystem<TaskStateComponent, DelayTaskComponent>` with `OnUpdate()`

- [ ] **Step 1: 写测试（先写会编译失败的测试，再补 DelayTaskComponent 让测试编译通过但行为失败）**

创建测试目录：

```bash
mkdir -p tests/Gameplay.Tests/GameplayTasks
```

```csharp
// tests/Gameplay.Tests/GameplayTasks/GameplayTaskTests.cs
using Friflo.Engine.ECS;
using Xunit;

namespace Gameplay.Tests;

public class GameplayTaskTests
{
    private static (World World, SystemRoot Root) Setup()
    {
        var world = new World(NetMode.Standalone);
        var root = new SystemRoot(world.Store) {
            new DelayTaskSystem(),
        };
        return (world, root);
    }

    private static Entity CreateDelayTask(EntityStore store, float duration)
    {
        var entity = store.CreateEntity();
        entity.AddComponent(new TaskOwnerComponent { Owner = default });
        entity.AddComponent(new TaskStateComponent { State = TaskState.Pending });
        entity.AddComponent(new DelayTaskComponent { Duration = duration, Elapsed = 0f });
        return entity;
    }

    [Fact]
    public void PendingTask_TransitionsToRunning_OnUpdate()
    {
        var (world, root) = Setup();
        var entity = CreateDelayTask(world.Store, 3f);

        root.Update(new UpdateTick(0.16f, 0));

        ref var state = ref entity.GetComponent<TaskStateComponent>();
        Assert.Equal(TaskState.Running, state.State);
    }

    [Fact]
    public void RunningTask_IncrementsElapsed_OnUpdate()
    {
        var (world, root) = Setup();
        var entity = CreateDelayTask(world.Store, 3f);

        root.Update(new UpdateTick(0.16f, 0)); // Pending → Running
        root.Update(new UpdateTick(0.16f, 0)); // Running, 累加 Elapsed

        ref var delay = ref entity.GetComponent<DelayTaskComponent>();
        Assert.Equal(0.16f, delay.Elapsed, 4);
    }

    [Fact]
    public void RunningTask_TransitionsToDone_WhenElapsedExceedsDuration()
    {
        var (world, root) = Setup();
        var entity = CreateDelayTask(world.Store, 0.3f);

        root.Update(new UpdateTick(0.16f, 0)); // Pending → Running
        root.Update(new UpdateTick(0.16f, 0)); // Elapsed=0.16
        root.Update(new UpdateTick(0.16f, 0)); // Elapsed=0.32 → Done

        ref var state = ref entity.GetComponent<TaskStateComponent>();
        Assert.Equal(TaskState.Done, state.State);
    }

    [Fact]
    public void DoneTask_StaysDone_OnUpdate()
    {
        var (world, root) = Setup();
        var entity = CreateDelayTask(world.Store, 0.1f);

        root.Update(new UpdateTick(0.16f, 0)); // Pending → Running
        root.Update(new UpdateTick(0.16f, 0)); // Elapsed=0.16 → Done
        root.Update(new UpdateTick(0.16f, 0)); // 再做一次 Tick

        ref var state = ref entity.GetComponent<TaskStateComponent>();
        Assert.Equal(TaskState.Done, state.State); // 仍为 Done
    }

    [Fact]
    public void CancelledTask_StaysCancelled_OnUpdate()
    {
        var (world, root) = Setup();
        var entity = CreateDelayTask(world.Store, 3f);
        entity.GetComponent<TaskStateComponent>().State = TaskState.Cancelled;

        root.Update(new UpdateTick(0.16f, 0));

        ref var state = ref entity.GetComponent<TaskStateComponent>();
        Assert.Equal(TaskState.Cancelled, state.State); // 未被 System 改变
    }

    [Fact]
    public void DoneTask_IsNotAutoDeleted()
    {
        var (world, root) = Setup();
        var entity = CreateDelayTask(world.Store, 0.1f);

        root.Update(new UpdateTick(0.16f, 0)); // Pending → Running
        root.Update(new UpdateTick(0.16f, 0)); // Elapsed=0.16 → Done

        Assert.True(world.Store.GetEntityById(entity.Id).Id == entity.Id);
    }

    [Fact]
    public void DurationZero_CompletesInOneFrame()
    {
        var (world, root) = Setup();
        var entity = CreateDelayTask(world.Store, 0f);

        root.Update(new UpdateTick(0.16f, 0)); // Pending → Running（Elapsed=0, Elapsed>=0 true → Done）

        ref var state = ref entity.GetComponent<TaskStateComponent>();
        Assert.Equal(TaskState.Done, state.State);
    }
}
```

- [ ] **Step 2: 运行测试确认失败**

```bash
dotnet test tests/Gameplay.Tests/Gameplay.Tests.csproj --filter "FullyQualifiedName~GameplayTaskTests" -f net10.0
```
预期：编译失败，`DelayTaskComponent` 和 `DelayTaskSystem` 类型不存在。

- [ ] **Step 3: 实现 DelayTaskComponent**

```csharp
// src/Gameplay/GameplayTasks/DelayTaskComponent.cs
using Friflo.Engine.ECS;

namespace Gameplay;

/// <summary>延时等待——累积 Elapsed 到达 Duration 后 Done。</summary>
public struct DelayTaskComponent : IComponent
{
    public float Duration;
    public float Elapsed;
}
```

- [ ] **Step 4: 实现 DelayTaskSystem**

```csharp
// src/Gameplay/GameplayTasks/DelayTaskSystem.cs
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

- [ ] **Step 5: 运行测试确认通过**

```bash
dotnet test tests/Gameplay.Tests/Gameplay.Tests.csproj --filter "FullyQualifiedName~GameplayTaskTests" -f net10.0
```
预期：7 tests PASS。

- [ ] **Step 6: 全量回归**

```bash
dotnet test tests/Gameplay.Tests/Gameplay.Tests.csproj -f net10.0
```
预期：48 tests PASS（41 GameplayTag + 7 GameplayTask）。

- [ ] **Step 7: 提交**

```bash
git add src/Gameplay/GameplayTasks/DelayTaskComponent.cs src/Gameplay/GameplayTasks/DelayTaskSystem.cs tests/Gameplay.Tests/GameplayTasks/GameplayTaskTests.cs
git commit -m "feat: DelayTaskComponent + DelayTaskSystem — 延时 Task 驱动"
```

---

### Task 3: 双 TFM 编译验证

**Files:** 无新建文件

- [ ] **Step 1: 编译 netstandard2.1**

```bash
dotnet build src/Gameplay/Gameplay.csproj -f netstandard2.1
```
预期：BUILD SUCCESS，0 warnings。

- [ ] **Step 2: 编译 net10.0**

```bash
dotnet build src/Gameplay/Gameplay.csproj -f net10.0
```
预期：BUILD SUCCESS，0 warnings。

- [ ] **Step 3: 运行全量测试**

```bash
dotnet test tests/Gameplay.Tests/Gameplay.Tests.csproj -f net10.0
```
预期：48 tests PASS。

---

## 完成标准

- [ ] 所有测试通过（48 tests）
- [ ] `dotnet build` 两个 TFM 全部通过，0 warnings
- [ ] `src/Gameplay/GameplayTasks/` 下 4 个文件
- [ ] `tests/Gameplay.Tests/GameplayTasks/` 下 1 个文件
