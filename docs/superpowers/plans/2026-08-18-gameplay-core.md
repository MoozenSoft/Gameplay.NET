# Gameplay.Core Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 构建 `Gameplay.Core` —— 纯 ECS Gameplay Runtime Kernel（World 中心化 + IModule + 调度 + 生命周期 + 事件总线 + 状态层 + 通用玩法）。

**Architecture:** 单程序集内的 `namespace Gameplay.Core`；`World` 聚合 `Store`/`ENetMode`/`Time`/`Events`/`Random` 五要素与唯一根调度器（`SystemRoot` 按 `ESimulationStage` 分组），模块经构造函数注入 `World` 挂载 System/Manager（`IModule` 为标记接口）；通用玩法组件（纯数据 struct）+ 系统（`QuerySystem`）分列 `Components/` 与 `Systems/`。

**Tech Stack:** .NET（`netstandard2.1` + `net10.0`）、Friflo.Engine.ECS 3.x、xUnit。

**Spec:** `docs/superpowers/specs/2026-08-18-gameplay-core-design.md`

## Global Constraints

- 文档/注释用**中文**，专业术语英文；枚举以 `E` 打头（`ETimeStep`、`ESimulationStage`、`EEntityLifecycleType`、`ENetMode` 均加 `E` 前缀）；枚举文件名不带 `E`（`NetMode.cs`/`TimeStep.cs`/`SimulationStage.cs`）。
- 私有字段 camelCase 无下划线前缀（`store` 而非 `_store`）；公开成员 PascalCase。
- 文件范围命名空间：`src/Gameplay/Gameplay.Core/` 下用 `namespace Gameplay.Core;`，子目录 `Components/`、`Systems/` 也用 `namespace Gameplay.Core;`（组件与系统都是 Core 顶层公开类型，不进子命名空间）。
- **Friflo IComponent 修改必须走 ref**：`entity.GetComponent<T>()` 返回 `ref`，`TryGetComponent<T>(out var)` 是值拷贝陷阱。
- **`QuerySystem.Query` 只在 `OnUpdate` 生命周期内有效**；事件回调（`EntityLifecycle`、`EventBus` handler）内用 `store.Query<>()` 遍历，禁止访问 `Query`。
- 0 GC：热路径（System 每帧遍历）严格；冷路径（注册、序列化）可放松。
- 组件为纯数据 struct（无行为方法）；System 持有全部逻辑。
- 测试命令：`dotnet test tests/Gameplay.Tests/Gameplay.Tests.csproj -f net10.0 --filter "FullyQualifiedName~ClassName"`；构建：`dotnet build src/Gameplay/Gameplay.csproj`。

---

### Task 1: 迁移 World + ENetMode 到 Gameplay.Core

**Files:**
- Create: `src/Gameplay/Gameplay.Core/World.cs`（内容同原 `src/Gameplay/World.cs`，namespace 改 `Gameplay.Core`，`NetMode` 类型名改 `ENetMode`）
- Create: `src/Gameplay/Gameplay.Core/NetMode.cs`（内容同原 `src/Gameplay/NetMode.cs`，namespace 改 `Gameplay.Core`，枚举名 `NetMode` 改 `ENetMode`）
- Delete: `src/Gameplay/World.cs`、`src/Gameplay/NetMode.cs`
- Modify: 下列 5 个文件补 `using Gameplay.Core;`
  - `src/Gameplay/Gameplay.Abilities/GameplayAbilitiesFeature.cs`
  - `tests/Gameplay.Tests/Gameplay.Tests.Abilities/GameplayAbilitiesFeatureTests.cs`
  - `tests/Gameplay.Tests/Gameplay.Tests.Tags/GameplayTagEdgeCaseTests.cs`
  - `tests/Gameplay.Tests/Gameplay.Tests.Tags/GameplayTagsTests.cs`
  - `tests/Gameplay.Tests/Gameplay.Tests.Tasks/DelaySystemTests.cs`

**Interfaces:**
- Consumes: 无（迁移起点）
- Produces: `Gameplay.Core.World`（`World(ENetMode)`、`ENetMode NetMode`、`EntityStore Store`、`GetNetMode()`）、`Gameplay.Core.ENetMode`（`Standalone/Client/DedicatedServer/ListenServer`）

- [ ] **Step 1: 创建 Gameplay.Core/World.cs**

在 `src/Gameplay/Gameplay.Core/World.cs` 写入（namespace 从 `Gameplay` 改为 `Gameplay.Core`，`NetMode` 类型名改为 `ENetMode`）：

```csharp
using Friflo.Engine.ECS;

namespace Gameplay.Core;

/// <summary>
/// 游戏世界，持有 ECS EntityStore 和网络模式信息。
/// </summary>
public class World
{
    private readonly EntityStore _store;

    /// <summary>当前网络模式。</summary>
    public ENetMode NetMode { get; }

    /// <summary>
    /// 创建指定网络模式下的游戏世界。
    /// </summary>
    public World(ENetMode netMode)
    {
        NetMode = netMode;
        _store = new EntityStore();
    }

    /// <summary>返回当前网络模式。</summary>
    public ENetMode GetNetMode() => NetMode;

    /// <summary>Friflo ECS 实体存储。</summary>
    public EntityStore Store => _store;
}
```

- [ ] **Step 2: 创建 Gameplay.Core/NetMode.cs**

在 `src/Gameplay/Gameplay.Core/NetMode.cs` 写入（namespace 改 `Gameplay.Core`，枚举名 `NetMode` 改 `ENetMode`）：

```csharp
namespace Gameplay.Core;

/// <summary>
/// 网络运行模式。
/// </summary>
public enum ENetMode
{
    /// <summary>单机模式（无网络）。</summary>
    Standalone,

    /// <summary>客户端模式。</summary>
    Client,

    /// <summary>专用服务器模式。</summary>
    DedicatedServer,

    /// <summary>监听服务器模式（Host）。</summary>
    ListenServer,
}
```

- [ ] **Step 3: 删除旧文件**

```bash
git rm src/Gameplay/World.cs src/Gameplay/NetMode.cs
```

- [ ] **Step 4: 补 using Gameplay.Core + 改枚举名**

对 5 个引用文件做两件事：① 在现有 `using` 区（`using Friflo...` 之后、命名空间声明之前）加入 `using Gameplay.Core;`；② 把 `NetMode` 类型引用改为 `ENetMode`（`NetMode.Standalone` → `ENetMode.Standalone` 等）。例（`DelaySystemTests.cs`）：

```csharp
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Gameplay;
using Gameplay.Core;      // ← 新增
using Gameplay.Tasks;
using Xunit;
```

其余 4 个文件同理（`GameplayAbilitiesFeature.cs`、`GameplayAbilitiesFeatureTests.cs`、`GameplayTagEdgeCaseTests.cs`、`GameplayTagsTests.cs`），并同步把其中的 `NetMode` 改为 `ENetMode`。

- [ ] **Step 5: 构建 + 全量测试通过**

Run: `dotnet build src/Gameplay/Gameplay.csproj`
Run: `dotnet test tests/Gameplay.Tests/Gameplay.Tests.csproj -f net10.0`
Expected: 构建成功；全部现有测试通过（迁移是 namespace 重命名 + 枚举名 `NetMode`→`ENetMode` 重命名，行为不变）。

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "迁移 World/ENetMode 到 Gameplay.Core 命名空间"
```

---

### Task 2: Vector3 + Quaternion 数学类型

**Files:**
- Create: `src/Gameplay/Gameplay.Core/Math/Vector3.cs`
- Create: `src/Gameplay/Gameplay.Core/Math/Quaternion.cs`
- Test: `tests/Gameplay.Tests/Gameplay.Tests.Core/Vector3Tests.cs`
- Test: `tests/Gameplay.Tests/Gameplay.Tests.Core/QuaternionTests.cs`

**Interfaces:**
- Consumes: 无
- Produces:
  - `Vector3`：`ctor(float x, float y, float z)`、字段 `X/Y/Z`、`static Vector3 Zero`、`+`、`-`、`*(float)`、`Dot(in Vector3, in Vector3)`、`LengthSquared()`、`Normalized()`、`IEquatable<Vector3>`
  - `Quaternion`：`ctor(float x, float y, float z, float w)`、字段 `X/Y/Z/W`、`static Quaternion Identity`、`IEquatable<Quaternion>`

- [ ] **Step 1: 写 Vector3 失败测试**

`tests/Gameplay.Tests/Gameplay.Tests.Core/Vector3Tests.cs`：

```csharp
using Gameplay.Core;
using Xunit;

namespace Gameplay.Tests.Core;

public class Vector3Tests
{
    [Fact]
    public void Add_ReturnsComponentWiseSum()
    {
        var a = new Vector3(1f, 2f, 3f);
        var b = new Vector3(4f, 5f, 6f);
        var c = a + b;
        Assert.Equal(5f, c.X);
        Assert.Equal(7f, c.Y);
        Assert.Equal(9f, c.Z);
    }

    [Fact]
    public void Scale_MultipliesEachComponent()
    {
        var a = new Vector3(1f, 2f, 3f);
        var c = a * 2f;
        Assert.Equal(2f, c.X);
        Assert.Equal(4f, c.Y);
        Assert.Equal(6f, c.Z);
    }

    [Fact]
    public void Dot_ReturnsScalarProduct()
    {
        var a = new Vector3(1f, 0f, 0f);
        var b = new Vector3(0f, 1f, 0f);
        Assert.Equal(0f, Vector3.Dot(in a, in b));
    }

    [Fact]
    public void Normalized_HasUnitLength()
    {
        var a = new Vector3(3f, 0f, 0f);
        var n = a.Normalized();
        Assert.Equal(1f, n.X, 4);
        Assert.Equal(0f, n.Y, 4);
    }
}
```

- [ ] **Step 2: 运行确认失败**

Run: `dotnet test tests/Gameplay.Tests/Gameplay.Tests.csproj -f net10.0 --filter "FullyQualifiedName~Vector3Tests"`
Expected: 编译错误（`Vector3` 未定义）。

- [ ] **Step 3: 实现 Vector3**

`src/Gameplay/Gameplay.Core/Math/Vector3.cs`：

```csharp
using System;

namespace Gameplay.Core;

/// <summary>自定义 3D 向量（跨 TFM 稳定、序列化友好）。</summary>
public readonly struct Vector3 : IEquatable<Vector3>
{
    public readonly float X;
    public readonly float Y;
    public readonly float Z;

    public Vector3(float x, float y, float z) { X = x; Y = y; Z = z; }

    public static Vector3 Zero => default;

    public static Vector3 operator +(in Vector3 a, in Vector3 b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
    public static Vector3 operator -(in Vector3 a, in Vector3 b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
    public static Vector3 operator *(in Vector3 a, float s) => new(a.X * s, a.Y * s, a.Z * s);

    public static float Dot(in Vector3 a, in Vector3 b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;

    public float LengthSquared() => X * X + Y * Y + Z * Z;

    public Vector3 Normalized()
    {
        var len = (float)Math.Sqrt(LengthSquared());
        return len <= 0f ? Zero : this * (1f / len);
    }

    public bool Equals(Vector3 other) => X == other.X && Y == other.Y && Z == other.Z;
    public override bool Equals(object? obj) => obj is Vector3 o && Equals(o);
    public override int GetHashCode() => HashCode.Combine(X, Y, Z);
    public override string ToString() => $"({X}, {Y}, {Z})";
}
```

- [ ] **Step 4: 写 Quaternion 失败测试**

`tests/Gameplay.Tests/Gameplay.Tests.Core/QuaternionTests.cs`：

```csharp
using Gameplay.Core;
using Xunit;

namespace Gameplay.Tests.Core;

public class QuaternionTests
{
    [Fact]
    public void Identity_HasW1ZeroXYZ()
    {
        var q = Quaternion.Identity;
        Assert.Equal(0f, q.X);
        Assert.Equal(0f, q.Y);
        Assert.Equal(0f, q.Z);
        Assert.Equal(1f, q.W);
    }

    [Fact]
    public void Equals_SameComponents_True()
    {
        var a = new Quaternion(1f, 2f, 3f, 4f);
        var b = new Quaternion(1f, 2f, 3f, 4f);
        Assert.True(a.Equals(b));
    }
}
```

- [ ] **Step 5: 运行确认失败**

Run: `dotnet test tests/Gameplay.Tests/Gameplay.Tests.csproj -f net10.0 --filter "FullyQualifiedName~QuaternionTests"`
Expected: 编译错误（`Quaternion` 未定义）。

- [ ] **Step 6: 实现 Quaternion**

`src/Gameplay/Gameplay.Core/Math/Quaternion.cs`：

```csharp
using System;

namespace Gameplay.Core;

/// <summary>自定义四元数（旋转，4 float）。</summary>
public readonly struct Quaternion : IEquatable<Quaternion>
{
    public readonly float X;
    public readonly float Y;
    public readonly float Z;
    public readonly float W;

    public Quaternion(float x, float y, float z, float w) { X = x; Y = y; Z = z; W = w; }

    public static Quaternion Identity => new(0f, 0f, 0f, 1f);

    public bool Equals(Quaternion other) => X == other.X && Y == other.Y && Z == other.Z && W == other.W;
    public override bool Equals(object? obj) => obj is Quaternion o && Equals(o);
    public override int GetHashCode() => HashCode.Combine(X, Y, Z, W);
    public override string ToString() => $"({X}, {Y}, {Z}, {W})";
}
```

- [ ] **Step 7: 运行全部通过**

Run: `dotnet test tests/Gameplay.Tests/Gameplay.Tests.csproj -f net10.0 --filter "FullyQualifiedName~Vector3Tests|FullyQualifiedName~QuaternionTests"`
Expected: 全部通过。

- [ ] **Step 8: Commit**

```bash
git add -A
git commit -m "添加 Vector3/Quaternion 数学类型"
```

---

### Task 3: GameTime + ETimeStep

**Files:**
- Create: `src/Gameplay/Gameplay.Core/Time/TimeStep.cs`
- Create: `src/Gameplay/Gameplay.Core/Time/GameTime.cs`
- Test: `tests/Gameplay.Tests/Gameplay.Tests.Core/GameTimeTests.cs`

**Interfaces:**
- Consumes: 无
- Produces:
  - `ETimeStep`（enum：`Variable`、`Fixed`）
  - `GameTime`：`ctor(ETimeStep mode)`、`void Advance(float deltaTime)`（推进 DeltaTime/ScaledDeltaTime/Tick）、属性 `float DeltaTime`、`float ScaledDeltaTime`、`float TimeScale`（get/set）、`bool IsPaused`（get/set）、`long Tick`、`ETimeStep Mode`

- [ ] **Step 1: 写失败测试**

`tests/Gameplay.Tests/Gameplay.Tests.Core/GameTimeTests.cs`：

```csharp
using Gameplay.Core;
using Xunit;

namespace Gameplay.Tests.Core;

public class GameTimeTests
{
    [Fact]
    public void Advance_UpdatesDeltaTimeAndTick()
    {
        var time = new GameTime(ETimeStep.Variable);
        time.Advance(0.16f);
        Assert.Equal(0.16f, time.DeltaTime);
        Assert.Equal(1, time.Tick);
    }

    [Fact]
    public void TimeScale_ScalesScaledDeltaTime()
    {
        var time = new GameTime(ETimeStep.Variable) { TimeScale = 0.5f };
        time.Advance(0.16f);
        Assert.Equal(0.16f, time.DeltaTime);
        Assert.Equal(0.08f, time.ScaledDeltaTime, 4);
    }

    [Fact]
    public void IsPaused_ZeroScaledDeltaTime()
    {
        var time = new GameTime(ETimeStep.Variable) { IsPaused = true };
        time.Advance(0.16f);
        Assert.Equal(0f, time.ScaledDeltaTime);
    }
}
```

- [ ] **Step 2: 运行确认失败**

Run: `dotnet test tests/Gameplay.Tests/Gameplay.Tests.csproj -f net10.0 --filter "FullyQualifiedName~GameTimeTests"`
Expected: 编译错误。

- [ ] **Step 3: 实现 ETimeStep + GameTime**

`src/Gameplay/Gameplay.Core/Time/TimeStep.cs`：

```csharp
namespace Gameplay.Core;

/// <summary>模拟步长模式。</summary>
public enum ETimeStep
{
    /// <summary>可变步长（每帧一次，dt 随渲染帧）。</summary>
    Variable,

    /// <summary>固定步长（累积器 + 可能多子步）。v1 仅占位，完整实现后置。</summary>
    Fixed,
}
```

`src/Gameplay/Gameplay.Core/Time/GameTime.cs`：

```csharp
namespace Gameplay.Core;

/// <summary>模拟时钟——所有 System 的时间基准。</summary>
public sealed class GameTime
{
    /// <summary>本帧（未缩放）步长。</summary>
    public float DeltaTime { get; private set; }

    /// <summary>时间缩放后的步长（受 <see cref="TimeScale"/> 与 <see cref="IsPaused"/> 影响）。</summary>
    public float ScaledDeltaTime { get; private set; }

    /// <summary>时间缩放（1 = 正常速度）。</summary>
    public float TimeScale { get; set; } = 1f;

    /// <summary>暂停时 ScaledDeltaTime 恒为 0。</summary>
    public bool IsPaused { get; set; }

    /// <summary>递增帧号。</summary>
    public long Tick { get; private set; }

    /// <summary>步长模式。</summary>
    public ETimeStep Mode { get; }

    public GameTime(ETimeStep mode) => Mode = mode;

    /// <summary>推进一帧。</summary>
    public void Advance(float deltaTime)
    {
        DeltaTime = deltaTime;
        ScaledDeltaTime = IsPaused ? 0f : deltaTime * TimeScale;
        Tick++;
    }
}
```

- [ ] **Step 4: 运行通过**

Run: `dotnet test tests/Gameplay.Tests/Gameplay.Tests.csproj -f net10.0 --filter "FullyQualifiedName~GameTimeTests"`
Expected: 全部通过。

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "添加 GameTime 模拟时钟"
```

---

### Task 4: DeterministicRng

**Files:**
- Create: `src/Gameplay/Gameplay.Core/Random/DeterministicRng.cs`
- Test: `tests/Gameplay.Tests/Gameplay.Tests.Core/DeterministicRngTests.cs`

**Interfaces:**
- Consumes: 无
- Produces: `DeterministicRng`：`ctor(ulong seed)`、`uint NextUInt()`、`float NextFloat()`、`int Range(int minInclusive, int maxExclusive)`、`ulong State { get; }`、`DeterministicRng Fork(int streamId)`

- [ ] **Step 1: 写失败测试**

`tests/Gameplay.Tests/Gameplay.Tests.Core/DeterministicRngTests.cs`：

```csharp
using Gameplay.Core;
using Xunit;

namespace Gameplay.Tests.Core;

public class DeterministicRngTests
{
    [Fact]
    public void SameSeed_ProducesSameSequence()
    {
        var a = new DeterministicRng(42UL);
        var b = new DeterministicRng(42UL);
        for (int i = 0; i < 100; i++)
            Assert.Equal(a.NextUInt(), b.NextUInt());
    }

    [Fact]
    public void DifferentSeed_ProducesDifferentSequence()
    {
        var a = new DeterministicRng(1UL);
        var b = new DeterministicRng(2UL);
        Assert.NotEqual(a.NextUInt(), b.NextUInt());
    }

    [Fact]
    public void NextFloat_IsInUnitInterval()
    {
        var rng = new DeterministicRng(7UL);
        for (int i = 0; i < 1000; i++)
        {
            var f = rng.NextFloat();
            Assert.InRange(f, 0f, 1f);
        }
    }

    [Fact]
    public void Fork_ProducesIndependentStream()
    {
        var rng = new DeterministicRng(42UL);
        var fork = rng.Fork(1);
        Assert.NotEqual(rng.NextUInt(), fork.NextUInt());
    }
}
```

- [ ] **Step 2: 运行确认失败**

Run: `dotnet test tests/Gameplay.Tests/Gameplay.Tests.csproj -f net10.0 --filter "FullyQualifiedName~DeterministicRngTests"`
Expected: 编译错误。

- [ ] **Step 3: 实现 SplitMix64**

`src/Gameplay/Gameplay.Core/Random/DeterministicRng.cs`：

```csharp
using System;

namespace Gameplay.Core;

/// <summary>确定性随机（SplitMix64，跨平台一致）。</summary>
public sealed class DeterministicRng
{
    private ulong _state;

    public DeterministicRng(ulong seed) => _state = seed;

    /// <summary>当前内部状态（快照/回放）。</summary>
    public ulong State => _state;

    /// <summary>下一个无符号 32 位随机数。</summary>
    public uint NextUInt()
    {
        _state += 0x9E3779B97F4A7C15UL;
        ulong z = _state;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        z ^= z >> 31;
        return (uint)(z & 0xFFFFFFFF);
    }

    /// <summary>[0,1) 区间的随机浮点数。</summary>
    public float NextFloat() => (NextUInt() >> 8) * (1f / 16777216f);

    /// <summary>[minInclusive, maxExclusive) 区间的随机整数。</summary>
    public int Range(int minInclusive, int maxExclusive)
    {
        if (maxExclusive <= minInclusive)
            throw new ArgumentOutOfRangeException(nameof(maxExclusive), "maxExclusive 必须大于 minInclusive");
        var range = (uint)(maxExclusive - minInclusive);
        return minInclusive + (int)(NextUInt() % range);
    }

    /// <summary>派生独立流（per-entity / per-system）。</summary>
    public DeterministicRng Fork(int streamId)
        => new(_state ^ (ulong)(streamId) * 0x9E3779B97F4A7C15UL);
}
```

- [ ] **Step 4: 运行通过**

Run: `dotnet test tests/Gameplay.Tests/Gameplay.Tests.csproj -f net10.0 --filter "FullyQualifiedName~DeterministicRngTests"`
Expected: 全部通过。

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "添加 DeterministicRng 确定性随机"
```

---

### Task 5: EventBus + IEvent + EntityDeathEvent

**Files:**
- Create: `src/Gameplay/Gameplay.Core/Event/IEvent.cs`
- Create: `src/Gameplay/Gameplay.Core/Event/EntityDeathEvent.cs`
- Create: `src/Gameplay/Gameplay.Core/Event/EventBus.cs`
- Test: `tests/Gameplay.Tests/Gameplay.Tests.Core/EventBusTests.cs`

**Interfaces:**
- Consumes: 无
- Produces:
  - `IEvent`（空标记接口）
  - `IEventHandler<T> where T : struct, IEvent`（`void Handle(in T evt)`）
  - `EntityDeathEvent`（`struct : IEvent`，字段 `Entity Entity`、`Entity Killer`）
  - `EventBus`：`void Enqueue<T>(in T evt)`、`void Subscribe<T>(IEventHandler<T>)`、`void Unsubscribe<T>(IEventHandler<T>)`、`void Tick()`

- [ ] **Step 1: 写失败测试**

`tests/Gameplay.Tests/Gameplay.Tests.Core/EventBusTests.cs`：

```csharp
using Friflo.Engine.ECS;
using Gameplay.Core;
using Xunit;

namespace Gameplay.Tests.Core;

public class EventBusTests
{
    private sealed class Counter : IEventHandler<EntityDeathEvent>
    {
        public int Count;
        public void Handle(in EntityDeathEvent evt) => Count++;
    }

    [Fact]
    public void Tick_DeliversEnqueuedEvent()
    {
        var bus = new EventBus();
        var handler = new Counter();
        bus.Subscribe<EntityDeathEvent>(handler);
        var store = new EntityStore();
        var entity = store.CreateEntity();

        bus.Enqueue(new EntityDeathEvent { Entity = entity });
        bus.Tick();

        Assert.Equal(1, handler.Count);
    }

    [Fact]
    public void Tick_DeliversToAllSubscribers()
    {
        var bus = new EventBus();
        var h1 = new Counter();
        var h2 = new Counter();
        bus.Subscribe<EntityDeathEvent>(h1);
        bus.Subscribe<EntityDeathEvent>(h2);
        var entity = new EntityStore().CreateEntity();

        bus.Enqueue(new EntityDeathEvent { Entity = entity });
        bus.Tick();

        Assert.Equal(1, h1.Count);
        Assert.Equal(1, h2.Count);
    }

    [Fact]
    public void Tick_NoEnqueue_NoDelivery()
    {
        var bus = new EventBus();
        var handler = new Counter();
        bus.Subscribe<EntityDeathEvent>(handler);
        bus.Tick();
        Assert.Equal(0, handler.Count);
    }

    [Fact]
    public void Unsubscribe_StopsDelivery()
    {
        var bus = new EventBus();
        var handler = new Counter();
        bus.Subscribe<EntityDeathEvent>(handler);
        bus.Unsubscribe<EntityDeathEvent>(handler);
        var entity = new EntityStore().CreateEntity();
        bus.Enqueue(new EntityDeathEvent { Entity = entity });
        bus.Tick();
        Assert.Equal(0, handler.Count);
    }
}
```

- [ ] **Step 2: 运行确认失败**

Run: `dotnet test tests/Gameplay.Tests/Gameplay.Tests.csproj -f net10.0 --filter "FullyQualifiedName~EventBusTests"`
Expected: 编译错误。

- [ ] **Step 3: 实现 IEvent + EntityDeathEvent**

`src/Gameplay/Gameplay.Core/Event/IEvent.cs`：

```csharp
namespace Gameplay.Core;

/// <summary>事件标记接口（EventBus 泛型约束）。</summary>
public interface IEvent { }

/// <summary>事件处理器。</summary>
public interface IEventHandler<T> where T : struct, IEvent
{
    void Handle(in T evt);
}
```

`src/Gameplay/Gameplay.Core/Event/EntityDeathEvent.cs`：

```csharp
using Friflo.Engine.ECS;

namespace Gameplay.Core;

/// <summary>实体死亡事件。</summary>
public readonly struct EntityDeathEvent : IEvent
{
    /// <summary>死亡的实体。</summary>
    public Entity Entity;

    /// <summary>击杀者（无击杀者为 null）。</summary>
    public Entity Killer;
}
```

- [ ] **Step 4: 实现 EventBus**

`src/Gameplay/Gameplay.Core/Event/EventBus.cs`：

```csharp
using System;
using System.Collections.Generic;

namespace Gameplay.Core;

/// <summary>Core 通用事件总线（双缓冲 + Tick 分发）。事件低频，接受装箱。</summary>
public sealed class EventBus
{
    private readonly Dictionary<Type, object> _queues = new();

    public void Enqueue<T>(in T evt) where T : struct, IEvent
        => GetQueue<T>().Pending.Add(evt);

    public void Subscribe<T>(IEventHandler<T> handler) where T : struct, IEvent
        => GetQueue<T>().Handlers.Add(handler);

    public void Unsubscribe<T>(IEventHandler<T> handler) where T : struct, IEvent
        => GetQueue<T>().Handlers.Remove(handler);

    /// <summary>每帧分发：交换 pending 帧并逐个派发给订阅者。</summary>
    public void Tick()
    {
        foreach (var box in _queues.Values)
            ((IEventQueue)box).Dispatch();
    }

    private EventQueue<T> GetQueue<T>() where T : struct, IEvent
    {
        if (_queues.TryGetValue(typeof(T), out var box))
            return (EventQueue<T>)box;
        var queue = new EventQueue<T>();
        _queues[typeof(T)] = queue;
        return queue;
    }

    private interface IEventQueue
    {
        void Dispatch();
    }

    private sealed class EventQueue<T> : IEventQueue where T : struct, IEvent
    {
        public readonly List<T> Pending = new();
        public readonly List<T> Processing = new();
        public readonly List<IEventHandler<T>> Handlers = new();

        private readonly List<IEventHandler<T>> _snapshot = new();

        public void Dispatch()
        {
            if (Pending.Count == 0) return;
            // swap：处理本帧之前入队的事件，分发中再 Enqueue 落入下一帧
            Processing.AddRange(Pending);
            Pending.Clear();
            _snapshot.Clear();
            _snapshot.AddRange(Handlers);   // 快照，分发中 Subscribe/Unsubscribe 不影响本次迭代
            for (int i = 0; i < Processing.Count; i++)
            {
                for (int h = 0; h < _snapshot.Count; h++)
                    _snapshot[h].Handle(in Processing[i]);
            }
            Processing.Clear();
        }
    }
}
```

- [ ] **Step 5: 运行通过**

Run: `dotnet test tests/Gameplay.Tests/Gameplay.Tests.csproj -f net10.0 --filter "FullyQualifiedName~EventBusTests"`
Expected: 全部通过。

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "添加 EventBus 通用事件总线"
```

---

### Task 6: EntityLifecycle 钩子封装

**Files:**
- Create: `src/Gameplay/Gameplay.Core/Lifecycle/EntityLifecycle.cs`
- Test: `tests/Gameplay.Tests/Gameplay.Tests.Core/EntityLifecycleTests.cs`

**Interfaces:**
- Consumes: `World`（Task 1）、Friflo `EntityStore.OnEntityCreate`/`OnEntityDelete`/`OnComponentAdded`/`OnComponentRemoved`
- Produces: `EntityLifecycle`（`static void Subscribe(World, EntityLifecycleHandler)`、`static void Unsubscribe(World, EntityLifecycleHandler)`）、`EntityLifecycleEvent`（`EEntityLifecycleType Type`、`Entity Entity`、`ComponentType ComponentType`）、`EEntityLifecycleType`（enum）、`delegate EntityLifecycleHandler(in EntityLifecycleEvent)`

- [ ] **Step 1: 写失败测试**

`tests/Gameplay.Tests/Gameplay.Tests.Core/EntityLifecycleTests.cs`：

```csharp
using Friflo.Engine.ECS;
using Gameplay.Core;
using Xunit;

namespace Gameplay.Tests.Core;

public class EntityLifecycleTests
{
    [Fact]
    public void Subscribe_ReceivesEntityCreatedEvent()
    {
        var world = new World(ENetMode.Standalone);
        EntityLifecycleEvent received = default;
        EntityLifecycle.Subscribe(world, (in EntityLifecycleEvent evt) => received = evt);

        var entity = world.Store.CreateEntity();

        Assert.Equal(EEntityLifecycleType.EntityCreated, received.Type);
        Assert.Equal(entity.Id, received.Entity.Id);
    }

    [Fact]
    public void Subscribe_ReceivesEntityDeletedEvent()
    {
        var world = new World(ENetMode.Standalone);
        EntityLifecycleEvent received = default;
        EntityLifecycle.Subscribe(world, (in EntityLifecycleEvent evt) => received = evt);

        var entity = world.Store.CreateEntity();
        entity.DeleteEntity();

        Assert.Equal(EEntityLifecycleType.EntityDeleted, received.Type);
        Assert.Equal(entity.Id, received.Entity.Id);
    }

    [Fact]
    public void Unsubscribe_StopsReceiving()
    {
        var world = new World(ENetMode.Standalone);
        int count = 0;
        EntityLifecycleHandler handler = (in EntityLifecycleEvent evt) => count++;
        EntityLifecycle.Subscribe(world, handler);
        EntityLifecycle.Unsubscribe(world, handler);

        world.Store.CreateEntity();

        Assert.Equal(0, count);
    }
}
```

- [ ] **Step 2: 运行确认失败**

Run: `dotnet test tests/Gameplay.Tests/Gameplay.Tests.csproj -f net10.0 --filter "FullyQualifiedName~EntityLifecycleTests"`
Expected: 编译错误。

- [ ] **Step 3: 实现 EntityLifecycle**

`src/Gameplay/Gameplay.Core/Lifecycle/EntityLifecycle.cs`：

```csharp
using System.Collections.Generic;
using Friflo.Engine.ECS;

namespace Gameplay.Core;

/// <summary>实体生命周期事件类型。</summary>
public enum EEntityLifecycleType
{
    EntityCreated,
    EntityDeleted,
    ComponentAdded,
    ComponentRemoved,
}

/// <summary>实体生命周期事件。</summary>
public readonly struct EntityLifecycleEvent
{
    public EEntityLifecycleType Type;
    public Entity Entity;
    public ComponentType ComponentType;   // 增删组件时有效
}

/// <summary>实体生命周期事件处理器。</summary>
public delegate void EntityLifecycleHandler(in EntityLifecycleEvent evt);

/// <summary>Friflo 实体事件的统一订阅面（即时转发，薄封装）。</summary>
public static class EntityLifecycle
{
    private sealed class HandlerList
    {
        public readonly List<EntityLifecycleHandler> Handlers = new();
        public bool Hooked;
    }

    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<EntityStore, HandlerList> HandlerMap = new();

    public static void Subscribe(World world, EntityLifecycleHandler handler)
    {
        var store = world.Store;
        var list = HandlerMap.GetOrCreateValue(store);
        list.Handlers.Add(handler);
        if (!list.Hooked)
        {
            list.Hooked = true;
            store.OnEntityCreate += OnEntityCreate;
            store.OnEntityDelete += OnEntityDelete;
            store.OnComponentAdded += OnComponentAdded;
            store.OnComponentRemoved += OnComponentRemoved;
        }
    }

    public static void Unsubscribe(World world, EntityLifecycleHandler handler)
    {
        if (!HandlerMap.TryGetValue(world.Store, out var list)) return;
        list.Handlers.Remove(handler);
    }

    private static void Dispatch(EntityStore store, in EntityLifecycleEvent evt)
    {
        if (!HandlerMap.TryGetValue(store, out var list)) return;
        foreach (var h in list.Handlers) h(in evt);
    }

    private static void OnEntityCreate(EntityCreate args)
        => Dispatch(args.Store, new EntityLifecycleEvent { Type = EEntityLifecycleType.EntityCreated, Entity = args.Entity });

    private static void OnEntityDelete(EntityDelete args)
        => Dispatch(args.Store, new EntityLifecycleEvent { Type = EEntityLifecycleType.EntityDeleted, Entity = args.Entity });

    private static void OnComponentAdded(ComponentChanged args)
        => Dispatch(args.Store, new EntityLifecycleEvent { Type = EEntityLifecycleType.ComponentAdded, Entity = args.Store.GetEntityById(args.EntityId), ComponentType = args.ComponentType });

    private static void OnComponentRemoved(ComponentChanged args)
        => Dispatch(args.Store, new EntityLifecycleEvent { Type = EEntityLifecycleType.ComponentRemoved, Entity = args.Store.GetEntityById(args.EntityId), ComponentType = args.ComponentType });
}
```

> **注意**：用 `ConditionalWeakTable<EntityStore, HandlerList>`（弱引用 `EntityStore`）而非强引用 `Dictionary<World,...>`，避免 World 订阅后无法 GC（spec 要求显式多 World 支持）。事件回调（`OnEntityCreate` 等）是 `static` 方法、不捕获 `world`，故不形成强引用环；`World` 丢弃后 `EntityStore` 可被 GC，`HandlerMap` 弱引用随之失效。

- [ ] **Step 4: 运行通过**

Run: `dotnet test tests/Gameplay.Tests/Gameplay.Tests.csproj -f net10.0 --filter "FullyQualifiedName~EntityLifecycleTests"`
Expected: 全部通过。

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "添加 EntityLifecycle 生命周期钩子封装"
```

---

### Task 7: 通用玩法组件（9 个）

**Files:**
- Create: `src/Gameplay/Gameplay.Core/Components/TransformComponent.cs`、`VelocityComponent.cs`、`TeamComponent.cs`、`PlayerStateComponent.cs`、`OwnerComponent.cs`、`HealthComponent.cs`、`SpawnPointComponent.cs`、`TimerComponent.cs`、`LifetimeComponent.cs`
- Test: `tests/Gameplay.Tests/Gameplay.Tests.Core/ComponentsTests.cs`

**Interfaces:**
- Consumes: `Vector3`/`Quaternion`（Task 2）、Friflo `IComponent`
- Produces: 9 个组件 struct（字段见 spec 第 12 节组件表）

- [ ] **Step 1: 写失败测试**

`tests/Gameplay.Tests/Gameplay.Tests.Core/ComponentsTests.cs`：

```csharp
using Friflo.Engine.ECS;
using Gameplay.Core;
using Xunit;

namespace Gameplay.Tests.Core;

public class ComponentsTests
{
    [Fact]
    public void Components_CanAttachToEntity()
    {
        var store = new EntityStore();
        var entity = store.CreateEntity();

        entity.AddComponent(new TransformComponent { Position = new Vector3(1f, 2f, 3f) });
        entity.AddComponent(new VelocityComponent { Velocity = new Vector3(0f, 1f, 0f) });
        entity.AddComponent(new HealthComponent { Current = 100f, Max = 100f, IsAlive = true });
        entity.AddComponent(new TeamComponent { TeamId = 1 });
        entity.AddComponent(new OwnerComponent { PlayerId = -1 });
        entity.AddComponent(new TimerComponent { Remaining = 3f, Duration = 3f });
        entity.AddComponent(new LifetimeComponent { Remaining = 5f });
        entity.AddComponent(new SpawnPointComponent { PrefabId = 1, TeamId = 1 });
        entity.AddComponent(new PlayerStateComponent { PlayerId = 1 });

        ref var health = ref entity.GetComponent<HealthComponent>();
        Assert.Equal(100f, health.Current);
        Assert.True(health.IsAlive);
    }

    [Fact]
    public void HealthComponent_Modification_RequiresRef()
    {
        var store = new EntityStore();
        var entity = store.CreateEntity();
        entity.AddComponent(new HealthComponent { Current = 100f, Max = 100f, IsAlive = true });

        ref var health = ref entity.GetComponent<HealthComponent>();
        health.Current = 0f;   // ref 写回生效

        Assert.Equal(0f, entity.GetComponent<HealthComponent>().Current);
    }
}
```

- [ ] **Step 2: 运行确认失败**

Run: `dotnet test tests/Gameplay.Tests/Gameplay.Tests.csproj -f net10.0 --filter "FullyQualifiedName~ComponentsTests"`
Expected: 编译错误。

- [ ] **Step 3: 实现 9 个组件**

`src/Gameplay/Gameplay.Core/Components/TransformComponent.cs`：

```csharp
using Friflo.Engine.ECS;

namespace Gameplay.Core;

/// <summary>空间变换。</summary>
public struct TransformComponent : IComponent
{
    public Vector3 Position;
    public Quaternion Rotation;
    public float Scale;
}
```

`src/Gameplay/Gameplay.Core/Components/VelocityComponent.cs`：

```csharp
using Friflo.Engine.ECS;

namespace Gameplay.Core;

/// <summary>速度（供 MovementSystem 积分）。</summary>
public struct VelocityComponent : IComponent
{
    public Vector3 Velocity;
}
```

`src/Gameplay/Gameplay.Core/Components/TeamComponent.cs`：

```csharp
using Friflo.Engine.ECS;

namespace Gameplay.Core;

/// <summary>阵营（未组队 = 0）。</summary>
public struct TeamComponent : IComponent
{
    public int TeamId;
}
```

`src/Gameplay/Gameplay.Core/Components/PlayerStateComponent.cs`：

```csharp
using Friflo.Engine.ECS;

namespace Gameplay.Core;

/// <summary>玩家身份（名字经 PlayerId 查外部表，Component 不存 string）。</summary>
public struct PlayerStateComponent : IComponent
{
    public int PlayerId;
}
```

`src/Gameplay/Gameplay.Core/Components/OwnerComponent.cs`：

```csharp
using Friflo.Engine.ECS;

namespace Gameplay.Core;

/// <summary>归属玩家（未归属 = -1）。</summary>
public struct OwnerComponent : IComponent
{
    public int PlayerId;
}
```

`src/Gameplay/Gameplay.Core/Components/HealthComponent.cs`：

```csharp
using Friflo.Engine.ECS;

namespace Gameplay.Core;

/// <summary>通用生命值 + 存活标记（死亡中间态）。</summary>
public struct HealthComponent : IComponent
{
    public float Current;
    public float Max;
    public bool IsAlive;
}
```

`src/Gameplay/Gameplay.Core/Components/SpawnPointComponent.cs`：

```csharp
using Friflo.Engine.ECS;

namespace Gameplay.Core;

/// <summary>一次性生成点（生成后移除）。</summary>
public struct SpawnPointComponent : IComponent
{
    public int PrefabId;
    public int TeamId;
}
```

`src/Gameplay/Gameplay.Core/Components/TimerComponent.cs`：

```csharp
using Friflo.Engine.ECS;

namespace Gameplay.Core;

/// <summary>通用计时/冷却。</summary>
public struct TimerComponent : IComponent
{
    public float Remaining;
    public float Duration;
    public bool Loop;
    public bool Completed;
}
```

`src/Gameplay/Gameplay.Core/Components/LifetimeComponent.cs`：

```csharp
using Friflo.Engine.ECS;

namespace Gameplay.Core;

/// <summary>存活倒计时（到期自动销毁）。</summary>
public struct LifetimeComponent : IComponent
{
    public float Remaining;
}
```

- [ ] **Step 4: 运行通过**

Run: `dotnet test tests/Gameplay.Tests/Gameplay.Tests.csproj -f net10.0 --filter "FullyQualifiedName~ComponentsTests"`
Expected: 全部通过。

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "添加通用玩法组件（9 个）"
```

---

### Task 8: IModule + ESimulationStage + World 调度扩展

**Files:**
- Create: `src/Gameplay/Gameplay.Core/Scheduling/SimulationStage.cs`
- Create: `src/Gameplay/Gameplay.Core/IModule.cs`
- Modify: `src/Gameplay/Gameplay.Core/World.cs`（加 `AddModule`/`AddSystem`/`RegisterService`/`GetService`/`DeferDelete`，内部持有 `SystemRoot` + 3 个 `SystemGroup`）
- Test: `tests/Gameplay.Tests/Gameplay.Tests.Core/WorldTests.cs`

**Interfaces:**
- Consumes: `World`（Task 1）、`GameTime`/`ETimeStep`（Task 3）、`DeterministicRng`（Task 4）、`EventBus`（Task 5）、Friflo `SystemRoot`/`SystemGroup`/`BaseSystem`
- Produces:
  - `ESimulationStage`（enum：`PreSimulation`、`Simulation`、`PostSimulation`）
  - `IModule`（`void Build(World world)`）
  - `World` 完整形态（一次性构建，Task 12 不再改 World）：`AddModule<T>()`/`AddModule(IModule)`/`AddSystem(BaseSystem, ESimulationStage)`/`RegisterService<T>(T)`/`GetService<T>()`/`DeferDelete(Entity)`/`Time`/`Events`/`Random`/`Update(float)`

- [ ] **Step 1: 写失败测试**

`tests/Gameplay.Tests/Gameplay.Tests.Core/WorldTests.cs`：

```csharp
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Gameplay.Core;
using Xunit;

namespace Gameplay.Tests.Core;

public class WorldTests
{
    private sealed class TestSystem : QuerySystem<HealthComponent>
    {
        public int RunCount;
        protected override void OnUpdate() => RunCount++;
    }

    private sealed class TestModule : IModule
    {
        public readonly TestSystem System = new();
        public void Build(World world) => world.AddSystem(System, ESimulationStage.Simulation);
    }

    [Fact]
    public void AddModule_InvokesBuild()
    {
        var world = new World(ENetMode.Standalone);
        var module = new TestModule();
        world.AddModule(module);
        Assert.NotNull(module.System);
    }

    [Fact]
    public void AddModuleGeneric_CreatesAndBuilds()
    {
        var world = new World(ENetMode.Standalone);
        world.AddModule<TestModule>();
        // 不抛异常即通过
    }

    [Fact]
    public void RegisterAndGetService_Roundtrips()
    {
        var world = new World(ENetMode.Standalone);
        var svc = new object();
        world.RegisterService(svc);
        Assert.Same(svc, world.GetService<object>());
    }

    [Fact]
    public void DeferDelete_DeletesOnUpdate()
    {
        var world = new World(ENetMode.Standalone);
        var entity = world.Store.CreateEntity();
        world.DeferDelete(entity);
        world.Update(0.16f);
        Assert.True(world.Store.GetEntityById(entity.Id).IsNull);
    }
}
```

- [ ] **Step 2: 运行确认失败**

Run: `dotnet test tests/Gameplay.Tests/Gameplay.Tests.csproj -f net10.0 --filter "FullyQualifiedName~WorldTests"`
Expected: 编译错误（`AddModule`/`DeferDelete`/`Update` 等未定义）。

- [ ] **Step 3: 实现 ESimulationStage + IModule**

`src/Gameplay/Gameplay.Core/Scheduling/SimulationStage.cs`：

```csharp
namespace Gameplay.Core;

/// <summary>模拟阶段。</summary>
public enum ESimulationStage
{
    PreSimulation,
    Simulation,
    PostSimulation,
}
```

`src/Gameplay/Gameplay.Core/IModule.cs`：

```csharp
namespace Gameplay.Core;

/// <summary>游戏世界模块——向 World 挂载 System/Manager。</summary>
public interface IModule
{
    void Build(World world);
}
```

- [ ] **Step 4: 扩展 World**

修改 `src/Gameplay/Gameplay.Core/World.cs`，加入调度器、模块挂载、服务注册、延迟删除：

```csharp
using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;

namespace Gameplay.Core;

public class World
{
    private readonly EntityStore _store;
    private readonly SystemRoot _root;
    private readonly SystemGroup _preGroup;
    private readonly SystemGroup _simGroup;
    private readonly SystemGroup _postGroup;
    private readonly Dictionary<Type, object> _services = new();
    private readonly List<Entity> _pendingDeletions = new();

    public ENetMode NetMode { get; }
    public EntityStore Store => _store;
    public GameTime Time { get; }
    public EventBus Events { get; }
    public DeterministicRng Random { get; }

    public World(ENetMode netMode, ulong seed = 0UL)
    {
        NetMode = netMode;
        _store = new EntityStore();
        Time = new GameTime(ETimeStep.Variable);
        Events = new EventBus();
        Random = new DeterministicRng(seed);
        _root = new SystemRoot(_store);
        _preGroup = new SystemGroup("PreSimulation");
        _simGroup = new SystemGroup("Simulation");
        _postGroup = new SystemGroup("PostSimulation");
        _root.Add(_preGroup);
        _root.Add(_simGroup);
        _root.Add(_postGroup);
    }

    public ENetMode GetNetMode() => NetMode;

    public World AddModule<T>() where T : IModule, new() => AddModule(new T());
    public World AddModule(IModule module)
    {
        module.Build(this);
        return this;
    }

    public void AddSystem(BaseSystem system, ESimulationStage stage)
    {
        var group = stage switch
        {
            ESimulationStage.PreSimulation => _preGroup,
            ESimulationStage.Simulation => _simGroup,
            ESimulationStage.PostSimulation => _postGroup,
            _ => _simGroup,
        };
        group.Add(system);
    }

    public void RegisterService<T>(T service) where T : class
        => _services[typeof(T)] = service;

    public T? GetService<T>() where T : class
        => _services.TryGetValue(typeof(T), out var box) ? (T)box : null;

    public void DeferDelete(Entity entity)
    {
        if (!entity.IsNull) _pendingDeletions.Add(entity);
    }

    public void Update(float deltaTime)
    {
        Time.Advance(deltaTime);                    // 1. 推进时钟
        Events.Tick();                              // 2. 分发事件（System 之前，本帧事件可见）
        _root.Update(new UpdateTick(Time.ScaledDeltaTime, 0)); // 3. 执行 System（Pre → Simulation → Post）
        ProcessPendingDeletions();                  // 4. 全局延迟删除
    }

    private void ProcessPendingDeletions()
    {
        for (int i = 0; i < _pendingDeletions.Count; i++)
        {
            var entity = _pendingDeletions[i];
            if (!entity.IsNull) entity.DeleteEntity();
        }
        _pendingDeletions.Clear();
    }
}
```

> **注意**：World 在本 Task 一次性构建完整（含 `Time`/`Events`/`Random` + 完整 `Update` 循环）。Task 12 仅做独立运行验证，不再改 World。

- [ ] **Step 5: 运行通过**

Run: `dotnet test tests/Gameplay.Tests/Gameplay.Tests.csproj -f net10.0 --filter "FullyQualifiedName~WorldTests"`
Expected: 全部通过。

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "World 添加 IModule 挂载与调度"
```

---

### Task 9: 通用玩法系统（4 个：Movement/Timer/Health/Lifetime）

**Files:**
- Create: `src/Gameplay/Gameplay.Core/Systems/MovementSystem.cs`、`TimerSystem.cs`、`HealthSystem.cs`、`LifetimeSystem.cs`
- Test: `tests/Gameplay.Tests/Gameplay.Tests.Core/MovementSystemTests.cs`、`TimerSystemTests.cs`、`HealthSystemTests.cs`、`LifetimeSystemTests.cs`

**Interfaces:**
- Consumes: 组件（Task 7）、`World.AddSystem`/`Events`（Task 8）、`EventBus`（Task 5，HealthSystem 用）、Friflo `QuerySystem`/`UpdateTick`
- Produces: 4 个 `QuerySystem`（Movement/Timer/Lifetime 无构造参数；`HealthSystem(EventBus events)` 构造注入事件总线）。SpawnSystem 依赖 `PrefabRegistry`，在 Task 10 实现。

- [ ] **Step 1: 写 MovementSystem 失败测试**

`tests/Gameplay.Tests/Gameplay.Tests.Core/MovementSystemTests.cs`：

```csharp
using Friflo.Engine.ECS.Systems;
using Gameplay.Core;
using Xunit;

namespace Gameplay.Tests.Core;

public class MovementSystemTests
{
    [Fact]
    public void Update_IntegratesVelocityIntoPosition()
    {
        var world = new World(ENetMode.Standalone);
        world.AddSystem(new MovementSystem(), ESimulationStage.Simulation);
        var entity = world.Store.CreateEntity();
        entity.AddComponent(new TransformComponent { Position = default, Scale = 1f });
        entity.AddComponent(new VelocityComponent { Velocity = new Vector3(1f, 0f, 0f) });

        world.Update(0.5f);

        ref var transform = ref entity.GetComponent<TransformComponent>();
        Assert.Equal(0.5f, transform.Position.X, 4);
    }
}
```

- [ ] **Step 2: 运行确认失败**

Run: `dotnet test tests/Gameplay.Tests/Gameplay.Tests.csproj -f net10.0 --filter "FullyQualifiedName~MovementSystemTests"`
Expected: 编译错误（`MovementSystem` 未定义）。

- [ ] **Step 3: 实现 MovementSystem**

`src/Gameplay/Gameplay.Core/Systems/MovementSystem.cs`：

```csharp
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;

namespace Gameplay.Core;

/// <summary>速度积分（pos += vel * dt）。</summary>
public sealed class MovementSystem : QuerySystem<TransformComponent, VelocityComponent>
{
    protected override void OnUpdate()
    {
        var dt = Tick.deltaTime;
        Query.ForEachEntity((ref TransformComponent transform, ref VelocityComponent velocity, Entity _) =>
        {
            transform.Position = transform.Position + velocity.Velocity * dt;
        });
    }
}
```

> **注意**：`QuerySystem` 的 `Tick` 属性来自 `BaseSystem`（当前帧 `UpdateTick`）。若 `Tick.deltaTime` 不可用（字段名需核实），用 `Tick.DeltaTime`；参照 Friflo `UpdateTick` 的实际字段名（`deltaTime`/`time`，见 `UpdateTick.cs`）。若字段为 `DeltaTime` 大写属性，相应调整。

- [ ] **Step 4: 写 TimerSystem / HealthSystem / LifetimeSystem 失败测试**

`tests/Gameplay.Tests/Gameplay.Tests.Core/TimerSystemTests.cs`：

```csharp
using Gameplay.Core;
using Xunit;

namespace Gameplay.Tests.Core;

public class TimerSystemTests
{
    [Fact]
    public void Update_DecrementsRemaining_AndSetsCompleted()
    {
        var world = new World(ENetMode.Standalone);
        world.AddSystem(new TimerSystem(), ESimulationStage.Simulation);
        var entity = world.Store.CreateEntity();
        entity.AddComponent(new TimerComponent { Remaining = 1f, Duration = 1f });

        world.Update(1.5f);

        ref var timer = ref entity.GetComponent<TimerComponent>();
        Assert.True(timer.Completed);
        Assert.True(timer.Remaining <= 0f);
    }
}
```

`tests/Gameplay.Tests/Gameplay.Tests.Core/HealthSystemTests.cs`：

```csharp
using Gameplay.Core;
using Xunit;

namespace Gameplay.Tests.Core;

public class HealthSystemTests
{
    [Fact]
    public void Update_ZeroHealth_MarksDead_AndEnqueuesDeath()
    {
        var world = new World(ENetMode.Standalone);
        var deaths = 0;
        world.Events.Subscribe<EntityDeathEvent>(new DeathCounter(() => deaths++));
        world.AddSystem(new HealthSystem(world.Events), ESimulationStage.Simulation);

        var entity = world.Store.CreateEntity();
        entity.AddComponent(new HealthComponent { Current = 0f, Max = 100f, IsAlive = true });

        world.Update(0.16f);   // HealthSystem 标记死亡 → CommandBuffer 删除 → enqueue
        world.Update(0.16f);   // Events.Tick 分发上一帧 enqueue 的死亡事件

        Assert.True(entity.IsNull);   // 实体已删除
        Assert.Equal(1, deaths);      // 死亡事件已分发
    }

    private sealed class DeathCounter : IEventHandler<EntityDeathEvent>
    {
        private readonly System.Action _onDeath;
        public DeathCounter(System.Action onDeath) => _onDeath = onDeath;
        public void Handle(in EntityDeathEvent evt) => _onDeath();
    }
}
```

`tests/Gameplay.Tests/Gameplay.Tests.Core/LifetimeSystemTests.cs`：

```csharp
using Gameplay.Core;
using Xunit;

namespace Gameplay.Tests.Core;

public class LifetimeSystemTests
{
    [Fact]
    public void Update_ExpiredLifetime_DeletesEntity()
    {
        var world = new World(ENetMode.Standalone);
        world.AddSystem(new LifetimeSystem(), ESimulationStage.Simulation);
        var entity = world.Store.CreateEntity();
        entity.AddComponent(new LifetimeComponent { Remaining = 0.5f });

        world.Update(1f);

        Assert.True(world.Store.GetEntityById(entity.Id).IsNull);
    }
}
```

- [ ] **Step 5: 运行确认失败**

Run: `dotnet test tests/Gameplay.Tests/Gameplay.Tests.csproj -f net10.0 --filter "FullyQualifiedName~TimerSystemTests|FullyQualifiedName~HealthSystemTests|FullyQualifiedName~LifetimeSystemTests"`
Expected: 编译错误。

- [ ] **Step 6: 实现 TimerSystem / HealthSystem / LifetimeSystem**

`src/Gameplay/Gameplay.Core/Systems/TimerSystem.cs`：

```csharp
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;

namespace Gameplay.Core;

/// <summary>计时递减，到期置 Completed。</summary>
public sealed class TimerSystem : QuerySystem<TimerComponent>
{
    protected override void OnUpdate()
    {
        var dt = Tick.deltaTime;
        Query.ForEachEntity((ref TimerComponent timer, Entity _) =>
        {
            if (timer.Completed) return;
            timer.Remaining -= dt;
            if (timer.Remaining <= 0f)
            {
                timer.Completed = true;
                if (timer.Loop)
                {
                    // while 循环处理 dt > Duration 的多圈 wrap（避免 Remaining 仍为负）
                    while (timer.Remaining <= 0f && timer.Duration > 0f)
                        timer.Remaining += timer.Duration;
                    timer.Completed = false;
                }
            }
        });
    }
}
```

`src/Gameplay/Gameplay.Core/Systems/HealthSystem.cs`：

```csharp
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;

namespace Gameplay.Core;

/// <summary>死亡判定：置 IsAlive=false → 广播 EntityDeathEvent → 延迟删除。</summary>
public sealed class HealthSystem : QuerySystem<HealthComponent>
{
    private readonly EventBus _events;

    public HealthSystem(EventBus events) => _events = events;

    protected override void OnUpdate()
    {
        var events = _events;
        Query.ForEachEntity((ref HealthComponent health, Entity entity) =>
        {
            if (!health.IsAlive || health.Current > 0f) return;
            health.IsAlive = false;   // 死亡中间态
            events.Enqueue(new EntityDeathEvent { Entity = entity });
            CommandBuffer.DeleteEntity(entity.Id);   // 经 CommandBuffer 帧末统一删除
        });
    }
}
```

> **注意**：`QuerySystem.OnUpdate` 内**禁止**直接 `entity.DeleteEntity()`（结构变更破坏 Query 遍历，违反 CLAUDE.md「Query 内不能 DeleteEntity」）。正确做法是 `CommandBuffer.DeleteEntity(entity.Id)`（`QuerySystemBase.CommandBuffer` 受保护属性），`SystemGroup.OnUpdateGroup` 末尾自动 `Playback()` 统一删除。

`src/Gameplay/Gameplay.Core/Systems/LifetimeSystem.cs`：

```csharp
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;

namespace Gameplay.Core;

/// <summary>存活倒计时，到期销毁。</summary>
public sealed class LifetimeSystem : QuerySystem<LifetimeComponent>
{
    protected override void OnUpdate()
    {
        var dt = Tick.deltaTime;
        Query.ForEachEntity((ref LifetimeComponent lifetime, Entity entity) =>
        {
            lifetime.Remaining -= dt;
            if (lifetime.Remaining <= 0f)
                CommandBuffer.DeleteEntity(entity.Id);   // 经 CommandBuffer 帧末统一删除
        });
    }
}
```

- [ ] **Step 7: 运行通过**

Run: `dotnet test tests/Gameplay.Tests/Gameplay.Tests.csproj -f net10.0 --filter "FullyQualifiedName~MovementSystemTests|FullyQualifiedName~TimerSystemTests|FullyQualifiedName~HealthSystemTests|FullyQualifiedName~LifetimeSystemTests"`
Expected: 全部通过。

- [ ] **Step 8: Commit**

```bash
git add -A
git commit -m "添加通用玩法系统（Movement/Timer/Health/Lifetime）"
```

---

### Task 10: Prefab + PrefabRegistry

**Files:**
- Create: `src/Gameplay/Gameplay.Core/Prefab/Prefab.cs`（含 `Prefab`/`PrefabBuilder`/`PrefabRegistry`）
- Create: `src/Gameplay/Gameplay.Core/Systems/SpawnSystem.cs`（按 `PrefabId` 实例化）
- Test: `tests/Gameplay.Tests/Gameplay.Tests.Core/PrefabTests.cs`、`SpawnSystemTests.cs`

**Interfaces:**
- Consumes: 组件（Task 7）、`World.AddSystem`（Task 8）、Friflo `QuerySystem`
- Produces:
  - `Prefab`（`static Prefab Define(Action<PrefabBuilder>)`、`Entity Instantiate(EntityStore store)`）
  - `PrefabBuilder`（`With<T>()`、`With<T>(in T)`）
  - `PrefabRegistry`（`static int Register(Prefab)`、`static Prefab? GetById(int id)`）
  - `SpawnSystem`（`QuerySystem<SpawnPointComponent>`，无构造参数，按 `PrefabId` 经 `PrefabRegistry.GetById` 实例化后移除 SpawnPoint）

- [ ] **Step 1: 写失败测试**

`tests/Gameplay.Tests/Gameplay.Tests.Core/PrefabTests.cs`：

```csharp
using Friflo.Engine.ECS;
using Gameplay.Core;
using Xunit;

namespace Gameplay.Tests.Core;

public class PrefabTests
{
    [Fact]
    public void Instantiate_CreatesEntityWithComponents()
    {
        var prefab = Prefab.Define(b => b
            .With(new HealthComponent { Current = 100f, Max = 100f, IsAlive = true })
            .With(new TeamComponent { TeamId = 1 }));

        var store = new EntityStore();
        var entity = prefab.Instantiate(store);

        Assert.True(entity.HasComponent<HealthComponent>());
        Assert.True(entity.HasComponent<TeamComponent>());
        Assert.Equal(1, entity.GetComponent<TeamComponent>().TeamId);
    }

    [Fact]
    public void Registry_RegisterAndGetById()
    {
        var prefab = Prefab.Define(b => b.With<HealthComponent>());
        var id = PrefabRegistry.Register(prefab);

        var got = PrefabRegistry.GetById(id);
        Assert.Same(prefab, got);
    }
}
```

- [ ] **Step 2: 运行确认失败**

Run: `dotnet test tests/Gameplay.Tests/Gameplay.Tests.csproj -f net10.0 --filter "FullyQualifiedName~PrefabTests"`
Expected: 编译错误。

- [ ] **Step 3: 实现 Prefab**

`src/Gameplay/Gameplay.Core/Prefab/Prefab.cs`：

```csharp
using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;

namespace Gameplay.Core;

/// <summary>组件模板条目（类型 + 初始值写回动作）。</summary>
internal readonly struct PrefabComponent
{
    public readonly ComponentType Type;
    public readonly Action<Entity> Apply;   // 实例化时写回组件值
}

/// <summary>Prefab 构建器。</summary>
public sealed class PrefabBuilder
{
    internal readonly List<PrefabComponent> Components = new();

    public PrefabBuilder With<T>() where T : struct, IComponent
    {
        Components.Add(new PrefabComponent
        {
            Type = ComponentType<T>.Value,
            Apply = e => e.AddComponent<T>(),
        });
        return this;
    }

    public PrefabBuilder With<T>(in T value) where T : struct, IComponent
    {
        Components.Add(new PrefabComponent
        {
            Type = ComponentType<T>.Value,
            Apply = e => e.AddComponent(value),
        });
        return this;
    }
}

/// <summary>Archetype 蓝图（纯数据模板）。</summary>
public sealed class Prefab
{
    private readonly PrefabComponent[] _components;

    private Prefab(PrefabComponent[] components) => _components = components;

    public static Prefab Define(Action<PrefabBuilder> config)
    {
        var builder = new PrefabBuilder();
        config(builder);
        return new Prefab(builder.Components.ToArray());
    }

    public Entity Instantiate(EntityStore store)
    {
        var entity = store.CreateEntity();
        foreach (var c in _components)
            c.Apply(entity);
        return entity;
    }
}

/// <summary>Prefab 全局注册中心（模板跨 World 共享，自增 id 索引）。</summary>
public static class PrefabRegistry
{
    private static readonly Dictionary<int, Prefab> ById = new();
    private static int _nextId = 1;

    public static int Register(Prefab prefab)
    {
        var id = _nextId++;
        ById[id] = prefab;
        return id;
    }

    public static Prefab? GetById(int id)
        => ById.TryGetValue(id, out var p) ? p : null;
}
```

> **注意**：`ComponentType<T>.Value` 是 Friflo 的组件类型常量；若实际 API 为 `ComponentTypes.Get<T>()` 或 `ComponentType<T>` 不同形态，按 Friflo `ComponentType` 实际 API 调整（`PrefabComponent.Type` 仅用于记录，`Apply` 委托才是实例化的关键）。若 `PrefabId` 需要，`PrefabRegistry` 可加 `int` 索引，v1 用 name 即可。

- [ ] **Step 4: 写 SpawnSystem 失败测试**

`tests/Gameplay.Tests/Gameplay.Tests.Core/SpawnSystemTests.cs`：

```csharp
using Friflo.Engine.ECS;
using Gameplay.Core;
using Xunit;

namespace Gameplay.Tests.Core;

public class SpawnSystemTests
{
    [Fact]
    public void Update_InstantiatesPrefab_AndRemovesSpawnPoint()
    {
        var prefab = Prefab.Define(b => b.With(new HealthComponent { Current = 50f, Max = 50f, IsAlive = true }));
        var prefabId = PrefabRegistry.Register(prefab);

        var world = new World(ENetMode.Standalone);
        world.AddSystem(new SpawnSystem(), ESimulationStage.Simulation);
        var spawnPoint = world.Store.CreateEntity();
        spawnPoint.AddComponent(new SpawnPointComponent { PrefabId = prefabId, TeamId = 1 });

        world.Update(0.16f);

        Assert.False(spawnPoint.HasComponent<SpawnPointComponent>());   // 一次性生成后移除
        var spawned = new Friflo.Engine.ECS.EntityStore[0];
        foreach (var e in world.Store.Query<HealthComponent>().Entities)
            spawned = new[] { e };
        Assert.NotNull(spawned[0]);
        Assert.Equal(50f, spawned[0].GetComponent<HealthComponent>().Current);
    }
}
```

> **注意**：`store.Query<HealthComponent>().Entities` 返回匹配实体的可遍历集合。若该 API 名不符（如 `Entities` 是 `ReadOnlySpan<Entity>`），改用 `foreach` + 计数器断言数量为 1。执行时以 Friflo `ArchetypeQuery` 实际成员为准微调。

- [ ] **Step 5: 运行确认失败**

Run: `dotnet test tests/Gameplay.Tests/Gameplay.Tests.csproj -f net10.0 --filter "FullyQualifiedName~SpawnSystemTests"`
Expected: 编译错误（`SpawnSystem` 未定义）。

- [ ] **Step 6: 实现 SpawnSystem**

`src/Gameplay/Gameplay.Core/Systems/SpawnSystem.cs`：

```csharp
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;

namespace Gameplay.Core;

/// <summary>按 PrefabId 实例化，一次性生成后移除 SpawnPoint。</summary>
public sealed class SpawnSystem : QuerySystem<SpawnPointComponent>
{
    private readonly System.Collections.Generic.List<(int PrefabId, int TeamId)> _pending = new();

    protected override void OnUpdate()
    {
        var store = Query.Store;
        _pending.Clear();
        Query.ForEachEntity((ref SpawnPointComponent spawnPoint, Entity entity) =>
        {
            _pending.Add((spawnPoint.PrefabId, spawnPoint.TeamId));
            CommandBuffer.RemoveComponent<SpawnPointComponent>(entity.Id);   // 一次性生成（经 CommandBuffer）
        });

        // 遍历结束后实例化（CreateEntity 不再影响已完成的 Query 遍历）
        for (int i = 0; i < _pending.Count; i++)
        {
            var (prefabId, teamId) = _pending[i];
            var prefab = PrefabRegistry.GetById(prefabId);
            if (prefab == null) continue;
            var spawned = prefab.Instantiate(store);
            if (teamId != 0)
                spawned.AddComponent(new TeamComponent { TeamId = teamId });
        }
    }
}
```

- [ ] **Step 7: 运行全部通过**

Run: `dotnet test tests/Gameplay.Tests/Gameplay.Tests.csproj -f net10.0 --filter "FullyQualifiedName~PrefabTests|FullyQualifiedName~SpawnSystemTests"`
Expected: 全部通过。

- [ ] **Step 8: Commit**

```bash
git add -A
git commit -m "添加 Prefab 蓝图、PrefabRegistry 与 SpawnSystem"
```

---

### Task 11: 组件序列化（ByteWriter/ByteReader/Serializer/EntitySnapshot）

**Files:**
- Create: `src/Gameplay/Gameplay.Core/Serialization/ByteWriter.cs`
- Create: `src/Gameplay/Gameplay.Core/Serialization/ByteReader.cs`
- Create: `src/Gameplay/Gameplay.Core/Serialization/IComponentSerializer.cs`
- Create: `src/Gameplay/Gameplay.Core/Serialization/SerializerRegistry.cs`
- Create: `src/Gameplay/Gameplay.Core/Serialization/EntitySnapshot.cs`
- Test: `tests/Gameplay.Tests/Gameplay.Tests.Core/SerializationTests.cs`

**Interfaces:**
- Consumes: `Vector3`/`Quaternion`（Task 2）、组件（Task 7）
- Produces:
  - `ByteWriter`（`ref struct`，`ctor(Span<byte>)`、`Write(int)`、`Write(float)`、`Write(bool)`、`Write(in Vector3)`、`Write(in Quaternion)`、`int BytesWritten`）
  - `ByteReader`（`ref struct`，`ctor(ReadOnlySpan<byte>)`、`ReadInt()`、`ReadFloat()`、`ReadBool()`、`ReadVector3()`、`ReadQuaternion()`）
  - `IComponentSerializer<T>`（`Write(in T, ref ByteWriter)`、`Read(ref T, ref ByteReader)`）
  - `SerializerRegistry`（`static void Register<T>(IComponentSerializer<T>)`、`static IComponentSerializer<T>? Get<T>()`）
  - `EntitySnapshot`（`static void Capture(Entity, ...)`、`static void Apply(Entity, ...)`）

- [ ] **Step 1: 写失败测试**

`tests/Gameplay.Tests/Gameplay.Tests.Core/SerializationTests.cs`：

```csharp
using System;
using Gameplay.Core;
using Xunit;

namespace Gameplay.Tests.Core;

public class SerializationTests
{
    [Fact]
    public void ByteWriter_WriteIntThenFloat_Roundtrips()
    {
        Span<byte> buf = stackalloc byte[64];
        var writer = new ByteWriter(buf);
        writer.Write(42);
        writer.Write(3.5f);

        var reader = new ByteReader(buf[..writer.BytesWritten]);
        Assert.Equal(42, reader.ReadInt());
        Assert.Equal(3.5f, reader.ReadFloat(), 4);
    }

    [Fact]
    public void ByteWriter_WriteVector3_Roundtrips()
    {
        Span<byte> buf = stackalloc byte[64];
        var writer = new ByteWriter(buf);
        var v = new Vector3(1f, 2f, 3f);
        writer.Write(in v);

        var reader = new ByteReader(buf[..writer.BytesWritten]);
        var got = reader.ReadVector3();
        Assert.Equal(v, got);
    }

    [Fact]
    public void EntitySnapshot_CaptureAndApply_Roundtrips()
    {
        var serializer = new HealthComponentSerializer();
        SerializerRegistry.Register(serializer);

        var world = new World(ENetMode.Standalone);
        var entity = world.Store.CreateEntity();
        entity.AddComponent(new HealthComponent { Current = 75f, Max = 100f, IsAlive = true });

        Span<byte> buf = stackalloc byte[256];
        var writer = new ByteWriter(buf);
        EntitySnapshot.Capture(entity, writer);
        // 修改原组件
        ref var health = ref entity.GetComponent<HealthComponent>();
        health.Current = 10f;

        var reader = new ByteReader(buf[..writer.BytesWritten]);
        EntitySnapshot.Apply(entity, ref reader);

        Assert.Equal(75f, entity.GetComponent<HealthComponent>().Current);
    }

    private sealed class HealthComponentSerializer : IComponentSerializer<HealthComponent>
    {
        public void Write(in HealthComponent c, ref ByteWriter w)
        {
            w.Write(c.Current);
            w.Write(c.Max);
            w.Write(c.IsAlive);
        }
        public void Read(ref HealthComponent c, ref ByteReader r)
        {
            c.Current = r.ReadFloat();
            c.Max = r.ReadFloat();
            c.IsAlive = r.ReadBool();
        }
    }
}
```

- [ ] **Step 2: 运行确认失败**

Run: `dotnet test tests/Gameplay.Tests/Gameplay.Tests.csproj -f net10.0 --filter "FullyQualifiedName~SerializationTests"`
Expected: 编译错误。

- [ ] **Step 3: 实现 ByteWriter / ByteReader**

`src/Gameplay/Gameplay.Core/Serialization/ByteWriter.cs`：

```csharp
using System;
using System.Runtime.InteropServices;

namespace Gameplay.Core;

/// <summary>序列化写入器（ref struct，栈语义）。</summary>
public ref struct ByteWriter
{
    private readonly Span<byte> _buffer;
    private int _position;

    public ByteWriter(Span<byte> buffer) { _buffer = buffer; _position = 0; }

    public int BytesWritten => _position;

    public void Write(int value) => WriteStruct(value);
    public void Write(float value) => WriteStruct(value);
    public void Write(bool value) => WriteStruct(value ? (byte)1 : (byte)0);
    public void Write(in Vector3 v) { Write(v.X); Write(v.Y); Write(v.Z); }
    public void Write(in Quaternion q) { Write(q.X); Write(q.Y); Write(q.Z); Write(q.W); }

    private void WriteStruct<T>(T value) where T : struct
    {
        var size = Marshal.SizeOf<T>();
        MemoryMarshal.Write(_buffer.Slice(_position, size), ref value);
        _position += size;
    }
}
```

`src/Gameplay/Gameplay.Core/Serialization/ByteReader.cs`：

```csharp
using System;
using System.Runtime.InteropServices;

namespace Gameplay.Core;

/// <summary>序列化读取器（ref struct）。</summary>
public ref struct ByteReader
{
    private readonly ReadOnlySpan<byte> _buffer;
    private int _position;

    public ByteReader(ReadOnlySpan<byte> buffer) { _buffer = buffer; _position = 0; }

    public int ReadInt() => ReadStruct<int>();
    public float ReadFloat() => ReadStruct<float>();
    public bool ReadBool() => ReadStruct<byte>() != 0;
    public Vector3 ReadVector3() => new(ReadFloat(), ReadFloat(), ReadFloat());
    public Quaternion ReadQuaternion() => new(ReadFloat(), ReadFloat(), ReadFloat(), ReadFloat());

    private T ReadStruct<T>() where T : struct
    {
        var size = Marshal.SizeOf<T>();
        var value = MemoryMarshal.Read<T>(_buffer.Slice(_position, size));
        _position += size;
        return value;
    }
}
```

- [ ] **Step 4: 实现 Serializer + Snapshot**

`src/Gameplay/Gameplay.Core/Serialization/IComponentSerializer.cs`：

```csharp
using Friflo.Engine.ECS;

namespace Gameplay.Core;

/// <summary>组件序列化器（组件 ↔ 数据）。</summary>
public interface IComponentSerializer<T> where T : struct, IComponent
{
    void Write(in T component, ref ByteWriter writer);
    void Read(ref T component, ref ByteReader reader);
}
```

`src/Gameplay/Gameplay.Core/Serialization/SerializerRegistry.cs`：

```csharp
using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;

namespace Gameplay.Core;

/// <summary>组件序列化器注册中心（static，程序级唯一映射，自增 typeId 索引）。</summary>
public static class SerializerRegistry
{
    private static readonly List<ISnapshotEntry> Entries = new();
    private static readonly Dictionary<Type, ISnapshotEntry> ByType = new();

    public static void Register<T>(IComponentSerializer<T> serializer) where T : struct, IComponent
    {
        var entry = new SnapshotEntry<T>(Entries.Count + 1, serializer);
        Entries.Add(entry);
        ByType[typeof(T)] = entry;
    }

    public static IComponentSerializer<T>? Get<T>() where T : struct, IComponent
        => ByType.TryGetValue(typeof(T), out var box) ? ((SnapshotEntry<T>)box).Serializer : null;

    internal static IReadOnlyList<ISnapshotEntry> EnumerateRegistered() => Entries;
    internal static ISnapshotEntry? GetByTypeId(int typeId)
        => typeId >= 1 && typeId <= Entries.Count ? Entries[typeId - 1] : null;
}
```

`src/Gameplay/Gameplay.Core/Serialization/EntitySnapshot.cs`：

```csharp
using Friflo.Engine.ECS;

namespace Gameplay.Core;

/// <summary>实体快照（组件编解码，不含网络）。</summary>
public static class EntitySnapshot
{
    /// <summary>捕获实体已注册组件到 buffer：写 [count][typeId+数据]*，Apply 按头读，组件集变化不错位。</summary>
    public static void Capture(Entity entity, ByteWriter writer)
    {
        var entries = SerializerRegistry.EnumerateRegistered();
        int count = 0;
        foreach (var entry in entries)
            if (entry.HasComponent(entity)) count++;
        writer.Write(count);
        foreach (var entry in entries)
        {
            if (!entry.HasComponent(entity)) continue;
            writer.Write(entry.TypeId);
            entry.Capture(entity, ref writer);
        }
    }

    public static void Apply(Entity entity, ref ByteReader reader)
    {
        int count = reader.ReadInt();
        for (int i = 0; i < count; i++)
        {
            int typeId = reader.ReadInt();
            SerializerRegistry.GetByTypeId(typeId)?.Apply(entity, ref reader);
        }
    }
}
```

> **注意**：`ISnapshotEntry` 是内部非泛型接口（含 `TypeId`），`IComponentSerializer<T>` 经泛型适配器 `SnapshotEntry<T>` 暴露非泛型能力。`EntitySnapshot` 写 `[count][typeId+数据]*` 头，`Apply` 按 typeId 查序列化器读回，因此组件集在 Capture 与 Apply 之间变化也不会错位。`Apply` 时若 entity 缺失某组件，先 `AddComponent<T>()` 再读（快照回放语义）。实现 `SerializerRegistry` 时补：

```csharp
internal interface ISnapshotEntry
{
    int TypeId { get; }
    bool HasComponent(Entity entity);
    void Capture(Entity entity, ref ByteWriter writer);
    void Apply(Entity entity, ref ByteReader reader);
}

internal sealed class SnapshotEntry<T> : ISnapshotEntry where T : struct, IComponent
{
    public int TypeId { get; }
    public IComponentSerializer<T> Serializer { get; }

    public SnapshotEntry(int typeId, IComponentSerializer<T> serializer) { TypeId = typeId; Serializer = serializer; }

    public bool HasComponent(Entity entity) => entity.HasComponent<T>();

    public void Capture(Entity entity, ref ByteWriter writer)
    {
        ref var c = ref entity.GetComponent<T>();
        Serializer.Write(in c, ref writer);
    }

    public void Apply(Entity entity, ref ByteReader reader)
    {
        if (!entity.HasComponent<T>())
            entity.AddComponent<T>();   // 快照里有但 entity 缺失 → 补上再读
        ref var c = ref entity.GetComponent<T>();
        Serializer.Read(ref c, ref reader);
    }
}
```

- [ ] **Step 5: 运行通过**

Run: `dotnet test tests/Gameplay.Tests/Gameplay.Tests.csproj -f net10.0 --filter "FullyQualifiedName~SerializationTests"`
Expected: 全部通过。

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "添加组件序列化（ByteWriter/EntitySnapshot）"
```

---

### Task 12: World 集成 + 独立运行验证

**Files:**
- Test: `tests/Gameplay.Tests/Gameplay.Tests.Core/WorldLifecycleTests.cs`

**Interfaces:**
- Consumes: `World` 完整形态（Task 8）、`MovementSystem`（Task 9）
- Produces: 无（纯验证任务，证明 Core 不带 GAS 可独立运行）

- [ ] **Step 1: 写失败测试**

`tests/Gameplay.Tests/Gameplay.Tests.Core/WorldLifecycleTests.cs`：

```csharp
using Gameplay.Core;
using Xunit;

namespace Gameplay.Tests.Core;

public class WorldLifecycleTests
{
    private sealed class MovementModule : IModule
    {
        public void Build(World world)
            => world.AddSystem(new MovementSystem(), ESimulationStage.Simulation);
    }

    [Fact]
    public void IndependentRun_MovementAdvancesWithoutGAS()
    {
        // 独立运行验证：Core 不带 GAS 可跑一个纯 ECS 世界
        var world = new World(ENetMode.Standalone);
        world.AddModule<MovementModule>();

        var entity = world.Store.CreateEntity();
        entity.AddComponent(new TransformComponent { Scale = 1f });
        entity.AddComponent(new VelocityComponent { Velocity = new Vector3(2f, 0f, 0f) });

        world.Update(0.16f);
        world.Update(0.16f);

        ref var transform = ref entity.GetComponent<TransformComponent>();
        Assert.Equal(0.64f, transform.Position.X, 4);
        Assert.Equal(2, world.Time.Tick);
    }

    [Fact]
    public void World_HasTimeEventsRandom()
    {
        var world = new World(ENetMode.Standalone);
        Assert.NotNull(world.Time);
        Assert.NotNull(world.Events);
        Assert.NotNull(world.Random);
    }
}
```

- [ ] **Step 2: 运行确认失败**

Run: `dotnet test tests/Gameplay.Tests/Gameplay.Tests.csproj -f net10.0 --filter "FullyQualifiedName~WorldLifecycleTests"`
Expected: 编译错误（`World.Time`/`Events`/`Random` 未定义）。

- [ ] **Step 3: 运行通过 + 全量回归**

Run: `dotnet test tests/Gameplay.Tests/Gameplay.Tests.csproj -f net10.0 --filter "FullyQualifiedName~WorldLifecycleTests"`
Run: `dotnet test tests/Gameplay.Tests/Gameplay.Tests.csproj -f net10.0`
Expected: 全部通过（含既有 Abilities/Tasks/Tags 测试，证明迁移未破坏行为）。

- [ ] **Step 4: Commit**

```bash
git add -A
git commit -m "添加 World 独立运行验证测试"
```

---

## Self-Review 备注

- **Spec 覆盖**：17 节 spec 的每个可交付项均有对应 Task —— 迁移（§14→Task1）、Vector3/Quaternion（§3→Task2）、GameTime（§5→Task3）、Rng（§9→Task4）、EventBus（§8→Task5）、EntityLifecycle（§7→Task6）、组件（§12→Task7）、IModule/调度/多 World（§4/§6→Task8）、系统（§13→Task9）、Prefab（§10→Task10）、序列化（§11→Task11）、World 集成+独立运行（§4/§15→Task12）。
- **已知 Friflo API 需执行时核实**（已标注在对应 Task 的「注意」）：`Tick.deltaTime` 字段名、`ComponentType<T>` 形态、`CommandBuffer` 删除语义。执行者若遇 API 名不符，以 Friflo 源码（`../Friflo.Engine.ECS/src/ECS/`）为准微调，不改变设计。
- **类型一致性**：`World`（`Store`/`ENetMode`/`Time`/`Events`/`Random` + `AddModule`/`AddSystem`/`RegisterService`/`GetService`/`DeferDelete`/`Update`）在 **Task 8 一次构建完整**，最终形态与 spec 第 4 节一致；组件字段名（`Position`/`Velocity`/`TeamId`/`PlayerId`/`Current`/`Max`/`IsAlive`/`PrefabId`/`Remaining`/`Duration`/`Loop`/`Completed`）跨 Task 一致；`SpawnSystem` 在 Task 10（依赖 `PrefabRegistry`）实现，非 Task 9。

---

## 实现演进记录（执行中偏离本 plan 原文的修正）

以下为实现阶段（task-review、final-review、`/code-review high`、命名规范审查）对 plan 原文的演进，spec 已同步。plan 的 TDD 步骤保留为历史记录，此处标注最终实现与原文的差异：

1. **IModule 接口**：plan 写「无参构造 + `Build(World)`」，实现改为「构造函数注入 `World`」的空标记接口（消除 `= null!` hack；`World.AddModule(IModule)` 只注册，去泛型 `AddModule<T>`）。
2. **World 双 Tick**：plan 写单 Tick，实现为双 Tick（`_root.Update` 后再 `Events.Tick()` 一次），死亡事件本帧分发、实体帧末才删（防 id 回收别名）。
3. **HealthSystem/LifetimeSystem 删除**：plan 写 `CommandBuffer.DeleteEntity`，实现改为构造注入 `Action<Entity> deferDelete`（指向 `World.DeferDelete`，`HashSet` 去重），防「一实体挂两个删除组件」时的双重删除崩溃。
4. **SpawnSystem 位置**：plan 只读 `PrefabId`/`TeamId`，实现 Query 加 `TransformComponent`，把 SpawnPoint 的 `Position` 传给新实体。
5. **HealthSystem 死亡判定**：plan 的 guard 依赖 `IsAlive`，实现改为 `Current <= 0`（`IsAlive` 是死亡中间态输出标记，非判定依据——消除「IsAlive 默认 false」footgun）。
6. **PlayerStateComponent**：plan 写 `PlayerId / Name`（string），实现仅 `PlayerId`（纯数据 struct，无 string）。
7. **序列化**：plan 写按注册顺序回放，实现写 `[count][typeId+数据]*` 头 + `Apply` 未知 typeId 抛异常（fail-fast）+ 重复 `Register` 替换保留原 id。
8. **每帧委托分配**：plan 写内联 lambda，实现改 `readonly ForEachEntity<...>` 字段缓存（64B/帧 → 0）。
9. **私有字段命名**：plan 原文用下划线前缀（`_store`），实现改 camelCase 无下划线（`store`），CLAUDE.md 已补「变量名/字段名不以 `_` 打头」。
10. **Abilities → Module**：plan 说 Phase 2 才重构，实际已完成（`GameplayAbilitiesFeature` → `GameplayAbilitiesModule : IModule`，构造注入 World，挂三阶段调度）。

**deferred（明确未做）**：`ETimeStep.Fixed` 完整实现、序列化 CodeGen、`SerializerRegistry` typeId 稳定 schema（跨进程同步前置）、`TimerComponent` 与 Tasks 同名并存（接受并存）。
