# GameplayAbilities Plan 3: GameplayEvent + GameplayCue + AbilityTask + Prediction

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 实现 P2 层——事件系统、表现系统、异步 Task、客户端预测

**Architecture:** GameplayEvent 走 SourceGenerator Event Schema + 双缓冲 EventBus（POCO）。GameplayCue 分 Static/Burst（消息通道）+ Looping（Entity）。AbilityTask 复用 Gameplay.Tasks 通用框架，加少量上下文。Prediction 三层架构：GAS 管 PredictionKey + IPredictionService 接口 → 网络层管 RPC → Bubble 实现。

**Tech Stack:** C# (.NET 10 / netstandard2.1), Friflo.Engine.ECS, xUnit

**Spec:** `docs/superpowers/specs/2026-07-21-gameplay-abilities-design.md`

**Plan 1-2 产物:** EffectSystem, AttributeSystem, AbilityActivationSystem, GameplayTagsComponent, GameplayAbility, AbilityCollectionComponent

## Global Constraints

- 命名空间：源码 `Gameplay.Abilities`，测试 `Gameplay.Tests.Abilities`
- 文档和注释用中文，专业术语用英文
- TDD：写测试 → 确认失败 → 写实现 → 确认通过 → 提交
- Friflo System 模式: `QuerySystem<T>` + `Query.ForEachEntity`
- Friflo 注册: `new SystemRoot(store) { sys1, sys2 }`
- 枚举以 `E` 打头
- 跨 TFM: `Enum.GetValues(typeof(T))` (非泛型), `new Random()` 代替 `Random.Shared`

---

## 文件结构

```
src/Gameplay/Gameplay.Abilities/
├── GameplayEvent/
│   ├── GameplayEventRecord.cs        # Event Record（EventId + Source + Target + Magnitude + PayloadIndex）
│   ├── StructBuffer.cs               # 通用 struct 缓冲（零 GC）
│   ├── GameplayEventFrame.cs         # 一帧事件的 Records + Payloads
│   ├── GameplayEventBus.cs           # 双缓冲 current/pending
│   ├── IGameplayEventHandler.cs      # Handler 接口 + Static/Dynamic Registry
│   └── EventSystem.cs                # 消费 Current → ID 匹配 → 分发
├── GameplayCue/
│   ├── GameplayCueManager.cs         # POCO, Static/Burst 消息通道 + Looping 管理
│   ├── GameplayCueParameters.cs      # Cue 参数
│   └── LoopingCueSystem.cs           # Looping Cue Entity Tick System
├── AbilityTask/
│   ├── AbilityTaskContextComponent.cs # 关联 ActiveAbility
│   ├── AbilityTaskSystem.cs          # Task 完成检测 + Cancel 传播
│   ├── WaitDelayTask.cs              # 等待 N 秒
│   ├── WaitGameplayEventTask.cs      # 等待特定 Event
│   ├── WaitCancelTask.cs             # 等待 Cancel
│   ├── WaitAttributeChangeTask.cs    # 等待属性变化 (P1)
│   ├── WaitGameplayTagTask.cs        # 等待 Tag 添加/移除 (P1)
│   └── WaitAbilityCommitTask.cs      # 等待 Commit 事件 (P1)
└── Prediction/
    ├── PredictionKey.cs              # 预测 Key 值类型
    ├── IPredictionService.cs         # Begin / Confirm / Reject 接口
    └── PredictionSystem.cs           # Confirm/Reject 实现 + Rollback

tests/Gameplay.Tests/Gameplay.Tests.Abilities/
├── GameplayEvent/
│   ├── StructBufferTests.cs
│   ├── GameplayEventBusTests.cs
│   └── EventSystemTests.cs
├── GameplayCue/
│   ├── GameplayCueManagerTests.cs
│   └── LoopingCueSystemTests.cs
├── AbilityTask/
│   ├── AbilityTaskContextComponentTests.cs
│   ├── AbilityTaskSystemTests.cs
│   ├── WaitDelayTaskTests.cs
│   ├── WaitGameplayEventTaskTests.cs
│   └── WaitCancelTaskTests.cs
└── Prediction/
    ├── PredictionKeyTests.cs
    └── PredictionSystemTests.cs
```

---

### Task 1: StructBuffer<T> — 通用无 GC Struct 缓冲

**Files:**
- Create: `src/Gameplay/Gameplay.Abilities/GameplayEvent/StructBuffer.cs`
- Create: `tests/Gameplay.Tests/Gameplay.Tests.Abilities/GameplayEvent/StructBufferTests.cs`

**Interfaces:**
- Produces: `StructBuffer<T> where T : unmanaged` — `Add(in T)`, `GetRef(int)`, `Reset()`, `Count`

- [ ] **Step 1: 写测试**

```csharp
namespace Gameplay.Tests.Abilities;

using Gameplay.Abilities;
using Xunit;

public class StructBufferTests
{
    [Fact]
    public void Add_IncreasesCount()
    {
        var buf = new StructBuffer<int>(); // or via factory
        buf.Add(10);
        buf.Add(20);
        Assert.Equal(2, buf.Count);
    }

    [Fact]
    public void GetRef_ReturnsCorrectValue()
    {
        var buf = new StructBuffer<float>();
        int idx = buf.Add(3.14f);
        Assert.Equal(3.14f, buf.GetRef(idx), 0.001f);
    }

    [Fact]
    public void Reset_ClearsCount()
    {
        var buf = new StructBuffer<int>();
        buf.Add(1);
        buf.Add(2);
        buf.Reset();
        Assert.Equal(0, buf.Count);
    }

    [Fact]
    public void Add_BeyondCapacity_Grows()
    {
        var buf = new StructBuffer<int>();
        for (int i = 0; i < 200; i++)
            buf.Add(i);
        Assert.Equal(200, buf.Count);
        Assert.Equal(150, buf.GetRef(150));
    }
}
```

- [ ] **Step 2-5: TDD cycle + commit**

```csharp
// StructBuffer.cs — 通用无 GC struct 缓冲
namespace Gameplay.Abilities;

public sealed class StructBuffer<T> where T : unmanaged
{
    private T[] buffer;
    private int count;

    public int Count => count;

    public int Add(in T value)
    {
        if (buffer == null) buffer = new T[16]; // 初始容量
        if (count >= buffer.Length) Array.Resize(ref buffer, buffer.Length * 2);
        buffer[count] = value;
        return count++;
    }

    public ref T GetRef(int index) => ref buffer[index];

    public void Reset() { count = 0; }  // 只重置计数，不清内存
}
```

---

### Task 2: GameplayEventBus — 双缓冲事件总线

**Files:**
- Create: `src/Gameplay/Gameplay.Abilities/GameplayEvent/GameplayEventRecord.cs`
- Create: `src/Gameplay/Gameplay.Abilities/GameplayEvent/GameplayEventFrame.cs`
- Create: `src/Gameplay/Gameplay.Abilities/GameplayEvent/GameplayEventBus.cs`
- Create: `tests/Gameplay.Tests/Gameplay.Tests.Abilities/GameplayEvent/GameplayEventBusTests.cs`

**Interfaces:**
- Consumes: `StructBuffer<T>` (Task 1)
- Produces: `GameplayEventRecord`, `GameplayEventFrame`, `GameplayEventBus` — 双缓冲 Enqueue/Consume

`GameplayEventRecord`:
```csharp
using Friflo.Engine.ECS;

namespace Gameplay.Abilities;

public struct GameplayEventRecord
{
    public ushort EventId;
    public Entity Source;
    public Entity Target;
    public float Magnitude;
    public int PayloadIndex;
}
```

`GameplayEventFrame`（每种 Event 一个 `StructBuffer`，当前简化版只用 `GameplayEventRecord[]` 作为 Payload）:
```csharp
namespace Gameplay.Abilities;

public class GameplayEventFrame
{
    public StructBuffer<GameplayEventRecord> Records = new();
    public void Reset() => Records.Reset();
}
```

`GameplayEventBus`:
```csharp
namespace Gameplay.Abilities;

public class GameplayEventBus
{
    private GameplayEventFrame current = new();
    private GameplayEventFrame pending = new();

    public void Enqueue(in GameplayEventRecord record) => pending.Records.Add(record);
    public GameplayEventFrame Swap() { (current, pending) = (pending, current); return current; }
}
```

- [ ] **Step 1: 写测试 — Enqueue + Swap**

```csharp
public class GameplayEventBusTests
{
    [Fact]
    public void Enqueue_GoesToPending()
    {
        var bus = new GameplayEventBus();
        bus.Enqueue(new GameplayEventRecord { EventId = 1, Magnitude = 5f });
        var frame = bus.Swap();
        Assert.Equal(1, frame.Records.Count);
        Assert.Equal(5f, frame.Records.GetRef(0).Magnitude, 0.001f);
    }

    [Fact]
    public void Swap_ReturnsPreviousPending()
    {
        var bus = new GameplayEventBus();
        bus.Enqueue(new GameplayEventRecord { EventId = 1 });
        var frame = bus.Swap();
        Assert.Equal(1, frame.Records.Count);

        // After swap, pending is empty, current has the event
        var frame2 = bus.Swap();
        Assert.Equal(0, frame2.Records.Count);
    }
}
```

- [ ] **Step 2-5: TDD + commit**

---

### Task 3: EventSystem — 消费事件 + Handler 机制

**Files:**
- Create: `src/Gameplay/Gameplay.Abilities/GameplayEvent/EventSystem.cs`
- Create: `src/Gameplay/Gameplay.Abilities/GameplayEvent/IGameplayEventHandler.cs`
- Create: `tests/Gameplay.Tests/Gameplay.Tests.Abilities/GameplayEvent/EventSystemTests.cs`

**Interfaces:**
- Consumes: `GameplayEventBus` (Task 2), `AbilityActivationSystem` (Plan 2)
- Produces: `EventSystem` (POCO), `IGameplayEventHandler` interface

```csharp
using System.Collections.Generic;
using Friflo.Engine.ECS;

namespace Gameplay.Abilities;

public interface IGameplayEventHandler
{
    void Handle(in GameplayEventRecord record);
}

public class EventSystem
{
    private readonly GameplayEventBus bus;
    private readonly Dictionary<ushort, List<IGameplayEventHandler>> staticHandlers = new();
    private readonly Dictionary<ushort, List<(int entityId, int handlerId)>> dynamicListeners = new();

    public EventSystem(GameplayEventBus bus) { this.bus = bus; }

    public void RegisterStatic(ushort eventId, IGameplayEventHandler handler) { ... }
    public void RegisterDynamic(ushort eventId, Entity owner, int handlerId) { ... }
    public void UnregisterDynamic(ushort eventId, Entity owner, int handlerId) { ... }

    public void Tick()
    {
        var frame = bus.Swap();
        for (int i = 0; i < frame.Records.Count; i++)
        {
            ref var record = ref frame.Records.GetRef(i);
            // Dispatch to static handlers
            if (staticHandlers.TryGetValue(record.EventId, out var handlers))
                foreach (var h in handlers) h.Handle(record);
            // Dispatch to dynamic listeners
            if (dynamicListeners.TryGetValue(record.EventId, out var listeners))
                foreach (var (owner, handlerId) in listeners)
                    InvokeDynamic(record, owner, handlerId);
        }
        frame.Reset();
    }
}
```

- [ ] **Step 1: 写测试 — register handler, enqueue event, tick**

```csharp
public class EventSystemTests
{
    [Fact]
    public void Tick_DispatchesStaticHandler()
    {
        var bus = new GameplayEventBus();
        var sys = new EventSystem(bus);
        var handler = new TestHandler();
        sys.RegisterStatic(1, handler);

        bus.Enqueue(new GameplayEventRecord { EventId = 1, Magnitude = 42f });
        sys.Tick();

        Assert.Equal(42f, handler.LastMagnitude, 0.001f);
    }

    private class TestHandler : IGameplayEventHandler
    {
        public float LastMagnitude;
        public void Handle(in GameplayEventRecord record) => LastMagnitude = record.Magnitude;
    }
}
```

- [ ] **Step 2-5: TDD + commit**

---

### Task 4: GameplayCueManager — 表现系统

**Files:**
- Create: `src/Gameplay/Gameplay.Abilities/GameplayCue/GameplayCueManager.cs`
- Create: `src/Gameplay/Gameplay.Abilities/GameplayCue/GameplayCueParameters.cs`
- Create: `tests/Gameplay.Tests/Gameplay.Tests.Abilities/GameplayCue/GameplayCueManagerTests.cs`

**Interfaces:**
- Consumes: `Gameplay.Tags` (GameplayTag) + `Friflo.Engine.ECS` (Entity)
- Produces: `GameplayCueManager` (POCO), `LoopingCueComponent` (IComponent)

Static/Burst Cue 走 POCO 消息通道，Looping Cue 走 Entity:

```csharp
using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Gameplay.Tags;

namespace Gameplay.Abilities;

public struct GameplayCueParameters
{
    public Entity Instigator;
    public float NormalizedMagnitude;
}

public class GameplayCueManager
{
    private readonly Dictionary<GameplayTag, Action<GameplayCueParameters>> staticHandlers = new();
    private readonly Dictionary<GameplayTag, Action<GameplayCueParameters>> burstHandlers = new();
    private readonly Dictionary<Entity, List<GameplayTag>> activeLoopingCues = new();

    public void RegisterStatic(GameplayTag tag, Action<GameplayCueParameters> handler);
    public void RegisterBurst(GameplayTag tag, Action<GameplayCueParameters> handler);

    public void AddCue(GameplayTag tag, GameplayCueParameters parameters, Entity target);
    public void RemoveCue(GameplayTag tag, Entity target);
    public void RemoveAllCues(Entity target);
}
```

- [ ] **Step 1: 写测试 — Register + AddCue + RemoveAllCues**
- [ ] **Step 2-5: TDD + commit**

---

### Task 5: AbilityTaskContext + AbilityTaskSystem

**Files:**
- Create: `src/Gameplay/Gameplay.Abilities/AbilityTask/AbilityTaskContextComponent.cs`
- Create: `src/Gameplay/Gameplay.Abilities/AbilityTask/AbilityTaskSystem.cs`
- Create: `tests/Gameplay.Tests/Gameplay.Tests.Abilities/AbilityTask/AbilityTaskSystemTests.cs`

**Interfaces:**
- Consumes: `ActiveAbilityComponent` (Plan 2), `TaskStateComponent` / `TaskOwnerComponent` (`Gameplay.Tasks` 现有)
- Produces: `AbilityTaskContextComponent`, `AbilityTaskSystem` (QuerySystem)

```csharp
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Gameplay.Tasks;

namespace Gameplay.Abilities;

public struct AbilityTaskContextComponent : IComponent
{
    public Entity ActiveAbility;
    public int TaskHandle;
}

// AbilityTaskSystem: 监听 ActiveAbility 下的 Task → 全部 Done → 触发 EndAbility
public class AbilityTaskSystem : QuerySystem<TaskStateComponent, AbilityTaskContextComponent>
{
    private readonly AbilityActivationSystem activationSystem;

    public AbilityTaskSystem(AbilityActivationSystem sys) { activationSystem = sys; }

    protected override void OnUpdate()
    {
        Query.ForEachEntity((ref TaskStateComponent state, ref AbilityTaskContextComponent ctx, Entity entity) =>
        {
            if (state.State == TaskState.Done || state.State == TaskState.Cancelled)
            {
                // 检查 ActiveAbility 下所有 Task 是否都 Done/Cancelled
                if (AllTasksDone(ctx.ActiveAbility))
                    activationSystem.CancelAbility(ctx.ActiveAbility);
            }
        });
    }

    // 遍历 ActiveAbility Entity 的所有子 Entity，检查 TaskState
    private bool AllTasksDone(Entity activeAbility)
    {
        var childEntities = activeAbility.ChildEntities;
        foreach (var child in childEntities)
        {
            if (child.TryGetComponent<TaskStateComponent>(out var ts))
            {
                if (ts.State != TaskState.Done && ts.State != TaskState.Cancelled)
                    return false;
            }
        }
        return true;
    }
}
```

- [ ] **Step 1: 写测试**
- [ ] **Step 2-5: TDD + commit**

---

### Task 6: 内置 AbilityTask — WaitDelay + WaitGameplayEvent + WaitCancel

**Files:**
- Create: `src/Gameplay/Gameplay.Abilities/AbilityTask/WaitDelayTask.cs`
- Create: `src/Gameplay/Gameplay.Abilities/AbilityTask/WaitGameplayEventTask.cs`
- Create: `src/Gameplay/Gameplay.Abilities/AbilityTask/WaitCancelTask.cs`
- Create: `tests/Gameplay.Tests/Gameplay.Tests.Abilities/AbilityTask/WaitDelayTaskTests.cs`
- Create: `tests/Gameplay.Tests/Gameplay.Tests.Abilities/AbilityTask/WaitGameplayEventTaskTests.cs`

**Interfaces:**
- Consumes: `Gameplay.Tasks` (TaskStateComponent, DelayTaskComponent), `EventSystem` (Task 3)
- Produces: 三个内置 Task Component + System

WaitDelayTask 直接复用现有 `DelayTaskComponent` + `DelayTaskSystem`。

WaitGameplayEventTask: Task Entity 挂 `TaskStateComponent` + `AbilityTaskContextComponent` + `WaitGameplayEventComponent` → 注册为 EventSystem Dynamic Listener → Event 触发 → Task Done。

WaitCancelTask: ActiveAbility 被 Cancel 时 → AbilityTaskSystem 级联 → 该 Task Done。

- [ ] **Step 1: 写测试**
- [ ] **Step 2-5: TDD + commit**

---

### Task 7: PredictionKey + IPredictionService + PredictionSystem

**Files:**
- Create: `src/Gameplay/Gameplay.Abilities/Prediction/PredictionKey.cs`
- Create: `src/Gameplay/Gameplay.Abilities/Prediction/IPredictionService.cs`
- Create: `src/Gameplay/Gameplay.Abilities/Prediction/PredictionSystem.cs`
- Create: `tests/Gameplay.Tests/Gameplay.Tests.Abilities/Prediction/PredictionKeyTests.cs`

**Interfaces:**
- Produces: `PredictionKey` struct, `IPredictionService` interface, `PredictionSystem`

```csharp
namespace Gameplay.Abilities;

public struct PredictionKey
{
    public int Key;
    public bool IsValid => Key > 0;
    public static PredictionKey Invalid => default;
}

public interface IPredictionService
{
    PredictionKey Begin();
    void Confirm(PredictionKey key);
    void Reject(PredictionKey key);
}

public class PredictionSystem
{
    private IPredictionService service;

    public void SetService(IPredictionService svc) => service = svc;

    public void Confirm(PredictionKey key)
    {
        // 找到所有带该 PredictionKey 的 Entity → 标记 Confirmed
    }

    public void Reject(PredictionKey key)
    {
        // 销毁预测 Entity + Aggregator 回滚
    }
}
```

- [ ] **Step 1: 写测试**
- [ ] **Step 2-5: TDD + commit**

---

## 验证

```bash
dotnet test tests/Gameplay.Tests/Gameplay.Tests.csproj -f net10.0
dotnet build src/Gameplay/Gameplay.csproj -f netstandard2.1
```
