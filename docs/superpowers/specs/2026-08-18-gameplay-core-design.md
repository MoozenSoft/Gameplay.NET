# Gameplay.Core 设计文档

日期：2026-08-18
状态：待评审

## 1. 概述

`Gameplay.Core` 是 Gameplay.NET 的 **ECS Gameplay Runtime Kernel**——各种类型游戏玩法的通用基础，基于 [Friflo.Engine.ECS](https://github.com/friflo/Friflo.Engine.ECS) 构建。

**核心目标**：管理游戏世界中的**状态（State）、行为（Behavior）、模拟生命周期（Simulation Lifecycle）**。

**定位**：
- 是 `Gameplay.Tasks`、`Gameplay.Abilities` 的**基础**（它们消费 Core，Core 不依赖它们）
- 可**独立运行**——不带 GAS（Abilities/Tasks/Tags）也能跑一个纯 ECS 世界
- 按**商业级游戏框架**标准设计（参照 Unity DOTS、Bevy、flecs 的 Core 分层）

## 2. 关键决策

| 决策点 | 结论 |
|--------|------|
| 程序集边界 | `Gameplay.dll` 内的 namespace（`namespace Gameplay.Core`），**不拆**独立 dll |
| 厚度 | **厚 kernel**——机制层 + 状态层 + 通用玩法层 |
| 架构方案 | **World 中心化**；模块接口命名为 `IModule`（原「Feature」弃用） |
| 确定性 | 提供**确定性随机**（`DeterministicRng`）；**不保证、不追求端到端确定性模拟（lockstep）** |
| 组件序列化 | Core 提供底层编解码 + 快照；状态同步模块（Bubble/回滚）在 Core 之上，Core 不含网络 |
| 现有模块迁移 | `Gameplay.Tags` / `Gameplay.Tasks` / `Gameplay.Interfaces` / `Gameplay.Utils` **全部不动**；仅 `World.cs` + `NetMode.cs` 迁入 |
| Abilities 改造 | v1 **只做最小 using 修补**，不重构为 `IModule`（Phase 2 任务） |
| 多 World | 显式支持（`World` 实例可多开）；实例级状态挂 World，类型级注册（Serializer/Prefab）`static` 共享 |
| 独立运行 | v1 用单测覆盖，不新增 samples 项目 |

### 2.1 确定性声明

Core 提供**确定性随机**，但**不保证端到端确定性模拟**。当前架构是**服务端权威 + Bubble/预测回滚**：

- 服务端是唯一真相源，客户端预测结果与权威端的 bit 级差异由**回滚机制兜底**，无需两个进程算出相同结果
- `DeterministicRng` 的真实用途：① 服务端下发 seed 供客户端预测生成相同随机值 → 减少回滚次数；② 回放（replay）；③ 可复现测试
- 若未来需要 lockstep 帧同步，是**独立的同步模型设计**，需另引入稳定遍历序、定点/严格浮点、确定性哈希等约束——不在 Core v1 范围

## 3. 目录结构与命名空间

`World.cs`、`NetMode.cs` 从 `src/Gameplay/` 顶层迁入，namespace 随目录改为 `Gameplay.Core`（遵循 CLAUDE.md「文件范围命名空间随目录走」约定）：

```
src/Gameplay/Gameplay.Core/
├── World.cs                    → namespace Gameplay.Core
├── NetMode.cs                  → namespace Gameplay.Core（从顶层迁入）
├── IModule.cs                  → IModule 接口 + Module 注册
├── Time/
│   ├── GameTime.cs             → 模拟时钟
│   └── TimeStep.cs             → 步长模式（Variable / Fixed）
├── Math/
│   ├── Vector3.cs              → 自定义 3D 向量（跨 TFM 稳定、序列化友好）
│   └── Quaternion.cs           → 自定义四元数（旋转，4 float）
├── Scheduling/
│   └── SimulationStage.cs      → Stage 枚举 + Stage 注册约定
├── Lifecycle/
│   └── EntityLifecycle.cs      → OnSpawn/OnDestroy/组件增删钩子
├── Event/
│   ├── EventBus.cs             → 通用事件总线（双缓冲 + Tick）
│   ├── IEvent.cs               → 事件标记接口 + IEventHandler
│   └── EntityDeathEvent.cs     → 死亡事件
├── Random/DeterministicRng.cs
├── Prefab/Prefab.cs            → Archetype 蓝图（含 PrefabBuilder/PrefabRegistry）
├── Serialization/
│   ├── IComponentSerializer.cs
│   ├── ByteWriter.cs / ByteReader.cs
│   ├── SerializerRegistry.cs
│   └── EntitySnapshot.cs
├── Components/                 → 通用玩法组件（纯数据 struct）
│   ├── TransformComponent.cs
│   ├── VelocityComponent.cs
│   ├── TeamComponent.cs
│   ├── PlayerStateComponent.cs
│   ├── OwnerComponent.cs
│   ├── HealthComponent.cs
│   ├── SpawnPointComponent.cs
│   ├── TimerComponent.cs
│   └── LifetimeComponent.cs
└── Systems/                    → 通用玩法系统
    ├── MovementSystem.cs
    ├── SpawnSystem.cs
    ├── TimerSystem.cs
    ├── HealthSystem.cs
    └── LifetimeSystem.cs
```

## 4. World 与 IModule

`World` 是**唯一生命周期入口**，聚合状态、时间、调度、模块：

```csharp
namespace Gameplay.Core;

/// <summary>游戏世界模块——向 World 挂载 System/Manager。Abilities、Combat、AI 都实现它。</summary>
public interface IModule
{
    void Build(World world);
}
```

```csharp
public class World
{
    public EntityStore Store { get; }     // 状态（第一版直接暴露，后续按需封装）
    public ENetMode NetMode { get; }
    public GameTime Time { get; }
    public EventBus Events { get; }       // Core 事件总线
    public DeterministicRng Random { get; } // 确定性随机（主 Rng，构造时可指定 seed）

    public World AddModule<T>() where T : IModule, new();
    public World AddModule(IModule module);

    // 模块用这两个 API 挂载行为
    public void AddSystem(BaseSystem system, ESimulationStage stage);
    public void RegisterService<T>(T service) where T : class;  // 注册 POCO Manager
    public T? GetService<T>() where T : class;                  // 取用已注册服务

    public void DeferDelete(Entity entity);   // 入全局延迟删除队列（Query 循环内不能 DeleteEntity）

    public void Update(float deltaTime);   // 唯一生命周期入口
}
```

**关键设计**：
- 一个 World 持有**唯一根调度器**；`AddModule` 立即调用 `IModule.Build(world)`，Module 把 System 挂到 World，由 World 统一排序执行
- 不再像现在 `GameplayAbilitiesFeature` 自己 new 一个 `SystemRoot`
- `Update(dt)` 固定顺序：`GameTime 推进 → EventBus.Tick()（分发上一帧事件）→ SystemRoot.Update（Stage：Pre → Simulation → Post）→ EventBus.Tick()（分发本帧事件，如死亡事件——实体此刻仍存活）→ 全局延迟删除`
- **多 World 支持**：`World` 是实例，可多开（分片/测试并行）；「实例级状态」（`DeterministicRng`、`GameTime`、Entity、调度）挂 World 实例，「类型级注册」（`SerializerRegistry`、`PrefabRegistry`）保持 `static` 共享
- **模块依赖靠顺序**：`AddModule` 按调用顺序立即 `Build`；模块 B 若取用模块 A 的服务，A 必须先 `AddModule`（v1 不引入显式依赖声明）
- `World` 仍暴露 `Store`（与现状一致），保证 `GameplayAbilitiesFeature` 的 `new GameplayAbilitiesFeature(store, netMode)` 最小修补后仍可用

## 5. GameTime 模拟时钟

```csharp
public sealed class GameTime
{
    public float DeltaTime { get; }          // 本帧（未缩放）步长
    public float ScaledDeltaTime { get; }    // 时间缩放后
    public float TimeScale { get; set; } = 1f;
    public bool IsPaused { get; set; }
    public long Tick { get; }                // 递增帧号
    public ETimeStep Mode { get; }            // Variable（每帧一次）| Fixed（固定步长，累积器 + 可能多子步）
}
```

- `World.Update(dt)` 开头推进 `GameTime`；System 从 `World.Time` 读时间，不再各自收 `UpdateTick`
- `ETimeStep.Fixed` 用**累积器**模式：逻辑固定步长（如 60 Hz），渲染帧可变，一帧内可能执行 0..N 个子步；v1 先实现 `Variable`，`Fixed` 提供接口但实现可后置

## 6. System 调度：ESimulationStage

把现在 `GameplayAbilitiesFeature.Update()` 手写的 Phase 0-5 形式化：

```csharp
public enum ESimulationStage
{
    PreSimulation,    // 模拟前准备（输入采集、模块前置初始化）——模块可挂前置工作
    Simulation,       // 主逻辑（Movement/Health/Timer/Spawn）
    PostSimulation,   // 模拟后收尾（延迟删除等）
}
```

- 每个 `ESimulationStage` 映射为 Friflo 一个 `SystemGroup`；`World.AddSystem(system, stage)` 挂到对应 group
- `World.Update` 的 SystemRoot 部分固定顺序：`Pre → Simulation → Post`，之后统一延迟删除，模块不用再手写

## 7. Entity 生命周期钩子

Friflo `EntityStore` 已提供底层事件（`OnEntityCreate` / `OnEntityDelete` / 组件增删），Core 封装成统一订阅面：

```csharp
public static class EntityLifecycle
{
    public static void Subscribe(World world, EntityLifecycleHandler handler);
    public static void Unsubscribe(World world, EntityLifecycleHandler handler);
}

public readonly struct EntityLifecycleEvent
{
    public EEntityLifecycleType Type;   // EntityCreated / EntityDeleted / ComponentAdded / ComponentRemoved
    public Entity Entity;
    public ComponentType ComponentType;   // 增删组件时有效
}
```

**关键设计**：
- 只做 **World 级全局订阅**（Friflo 原生能力，薄封装）；per-entity 钩子靠 Component 组合 + System 表达（数据驱动），不做 per-entity 钩子
- **即时转发**：直接订阅 Friflo `OnEntityCreate`/`OnEntityDelete`/组件增删事件，回调在 **Update 之外**即时触发（`World.Update` 不设「钩子分发」步骤），与 CLAUDE.md「事件回调内必须用 `store.Query<>()`」约束一致——消费方须注意，禁止在此访问 `QuerySystem.Query`
- `OnDestroy` 清理逻辑统一由 lifecycle 钩子驱动

## 8. 事件总线（EventBus）

Core 的**通用事件总线**——事件驱动的平台基础设施（跨系统通信不直接耦合）。与 GAS 的 `GameplayEventBus`（`Gameplay.Abilities`）**并存、互不依赖**（v1 不动 Abilities 的事件总线，Phase 2 视需要再评估合并）。

```csharp
namespace Gameplay.Core;

/// <summary>Core 通用事件总线（双缓冲 + Tick 分发）。</summary>
public sealed class EventBus
{
    public void Enqueue<T>(in T evt) where T : struct, IEvent;
    public void Subscribe<T>(IEventHandler<T> handler) where T : struct, IEvent;
    public void Unsubscribe<T>(IEventHandler<T> handler) where T : struct, IEvent;
    public void Tick();   // 每帧分发（World.Update 在 SystemRoot 之前调用）
}

public interface IEvent { }  // 事件标记接口

public interface IEventHandler<T> where T : struct, IEvent
{
    void Handle(in T evt);
}
```

**核心事件**：

```csharp
public readonly struct EntityDeathEvent : IEvent
{
    public Entity Entity;
    public Entity Killer;   // 可选，无击杀者时为 null
}
```

**关键设计**：
- **双缓冲**：`Enqueue` 写 pending 帧，`Tick` 交换并分发（避免分发中 Enqueue 的迭代问题）
- **泛型 struct 事件**（类型安全）；订阅用 `IEventHandler<T>` 接口（避免委托闭包 GC）
- `World.Update` 在 SystemRoot 之前调用 `EventBus.Tick()`（本帧事件对 System 可见，对应 GAS 的 Phase 0）
- **与 `EntityLifecycle` 区分**：`EntityLifecycle` 是 Friflo 原生事件（Entity 创建/删除/组件增删）的即时转发；`EventBus` 是**逻辑事件**（死亡、拾取等业务语义）的双缓冲分发

## 9. DeterministicRng 确定性随机

```csharp
public sealed class DeterministicRng
{
    public DeterministicRng(ulong seed);
    public uint NextUInt();
    public float NextFloat();                  // [0,1)
    public int Range(int minInclusive, int maxExclusive);
    public ulong State { get; }                // 可读取当前状态（快照/重放）
    public DeterministicRng Fork(int streamId); // 派生独立流（per-entity / per-system）
}
```

- 算法：`SplitMix64` 或 `xoshiro256**`，跨平台一致（无 `System.Random` 历史实现差异）
- **per-World 实例**：每个 `World` 挂一个主 Rng（实例字段，非 `static`）；`Fork(streamId)` 供 per-System/per-Entity 独立序列
- `State` 可序列化 → 接入 `EntitySnapshot` 实现确定性回放

## 10. Prefab 蓝图

数据驱动的 Entity 批量创建模板：

```csharp
public sealed class Prefab
{
    public static Prefab Define(Action<PrefabBuilder> config);
    public Entity Instantiate(World world, in TransformComponent? spawn = null);
}

public sealed class PrefabBuilder
{
    public PrefabBuilder With<T>() where T : struct, IComponent;
    public PrefabBuilder With<T>(in T value) where T : struct, IComponent;
    public PrefabBuilder WithChild(string name, Action<PrefabBuilder> child);
}
```

- `Prefab` 是**纯数据模板**（组件类型集合 + 默认值），`Instantiate` 按模板一次创建并写组件
- `SpawnSystem` 依赖 `Prefab`：`SpawnPointComponent` 持有 `PrefabId`（int），经 `PrefabRegistry` 查找（组件只存标识，不存对象引用）
- `PrefabRegistry`（name → Prefab）`static` 全局注册（模板跨 World 共享），配置层（JSON）后续接入

## 11. 组件序列化（快照底层）

Core 只提供「组件 ↔ 数据」编解码，**不含网络**：

```csharp
public interface IComponentSerializer<T> where T : struct, IComponent
{
    void Write(in T component, ref ByteWriter writer);
    void Read(ref T component, ref ByteReader reader);
}

public static class EntitySnapshot
{
    public static void Capture(Entity entity, ReadOnlySpan<ComponentType> types, ref ByteWriter output);
    public static void Apply(Entity entity, ref ByteReader reader);
}
```

- 序列化器按组件类型**手动注册**（`SerializerRegistry.Register<T>(serializer)`）；CodeGen 自动生成是后续增强，非 v1
- `SerializerRegistry` 是 `static` 全局（组件类型→序列化器为程序级唯一映射，与 World 无关）
- `ByteWriter`/`ByteReader`：`ref struct`（编译器强制栈语义，不逃逸堆）+ 三层后端——栈上 `stackalloc`（小体积零分配）、`ArrayPool` 租借（大体积 `finally` 归还）、帧级 arena（Phase 2 批量同步）
- 未来的状态同步模块（Bubble/回滚）在 `EntitySnapshot` 之上构建

## 12. 通用玩法组件

纯数据 struct（无行为方法），位于 `Gameplay.Core` 的 `Components/`：

| 组件 | 字段 | 说明 |
|------|------|------|
| `TransformComponent` | `Position`（`Vector3`）/ `Rotation`（`Quaternion`）/ `Scale` | 空间变换（`Vector3`、`Quaternion` 为自定义类型） |
| `VelocityComponent` | `Velocity`（`Vector3`） | 供 `MovementSystem` 积分 |
| `TeamComponent` | `TeamId`（int，未组队 = 0） | 阵营，友伤过滤/目标选择 |
| `PlayerStateComponent` | `PlayerId`（int） | 玩家身份（名字经 PlayerId 查外部表，Component 不存 string） |
| `OwnerComponent` | `PlayerId`（int，未归属 = -1） | 归属玩家，输入路由/相关性/预测归属 |
| `HealthComponent` | `Current` / `Max` / `IsAlive` | 通用生命值 + 存活标记（死亡中间态） |
| `SpawnPointComponent` | `PrefabId`（int）/ `TeamId` | 一次性生成点（生成后移除；存 Prefab 标识，非对象引用） |
| `TimerComponent` | `Remaining` / `Duration` / `Loop` / `Completed` | 通用计时/冷却 |
| `LifetimeComponent` | `Remaining` | 存活倒计时，到期自动销毁 |

## 13. 通用玩法系统

位于 `Gameplay.Core` 的 `Systems/`，全部挂 Stage `Simulation`：

| 系统 | Query | 逻辑 |
|------|-------|------|
| `MovementSystem` | `Velocity + Transform` | 速度积分（`pos += vel * dt`） |
| `TimerSystem` | `Timer` | 递减，到期置 `Completed = true`（数据驱动，消费方 System 检测，无 delegate 回调） |
| `SpawnSystem` | `SpawnPoint` | 按 `PrefabId` 实例化 Entity，一次性生成后移除 SpawnPoint |
| `HealthSystem` | `Health` | `Current <= 0` → 置 `IsAlive = false`（死亡中间态）→ `EventBus.Enqueue(EntityDeathEvent)` → `World.DeferDelete` → 帧末真正删除 |
| `LifetimeSystem` | `Lifetime` | 递减 `Remaining`，到期入延迟删除队列 → 帧末销毁 |

> 注意：Core 的 `MovementSystem`/`TimerSystem`/`SpawnSystem` 是**全新实现**，与 `Gameplay.Tasks` 里既有的 `MoveToSystem`/`TimerSystem`/`SpawnSystem`/`DelaySystem` **并存、互不依赖**（Tasks 一个文件不动）。

## 14. 现有代码迁移策略

| 现有 | 去向 |
|------|------|
| `src/Gameplay/World.cs`（`namespace Gameplay`） | **迁入** `Gameplay.Core/World.cs`（`namespace Gameplay.Core`） |
| `src/Gameplay/NetMode.cs`（`namespace Gameplay`） | **迁入** `Gameplay.Core/NetMode.cs`（`namespace Gameplay.Core`） |
| `Gameplay.Abilities`（`GameplayAbilitiesFeature` 等） | **v1 不动**，仅因 ENetMode 迁 namespace 补 `using Gameplay.Core;` |
| `Gameplay.Tasks` | **一个文件都不动** |
| `Gameplay.Tags` | 不动 |
| `Gameplay.Interfaces`（`IInputService`） | 不动 |
| `Gameplay.Utils` | 不动 |

**连锁影响**：`ENetMode` 迁 namespace 后，所有引用它的调用点需补 `using Gameplay.Core;`：
- `src/Gameplay/Gameplay.Abilities/GameplayAbilitiesFeature.cs`（构造函数参数 `ENetMode`）
- 测试项目 `tests/Gameplay.Tests/` 中 4 个测试文件（`GameplayTagEdgeCaseTests`、`DelaySystemTests`、`GameplayAbilitiesFeatureTests`、`GameplayTagsTests`）
- `World` 迁 namespace 后同理会连锁（已确认 `samples/` 不引用 `World`/`ENetMode`，不受影响）

`World` 仍暴露 `Store`（`public EntityStore Store`），保证 `GameplayAbilitiesFeature(store, netMode)` 的现有调用方式不变。

## 15. 测试与独立运行

```
tests/Gameplay.Tests/Gameplay.Tests.Core/   ← 新目录
    WorldTests.cs            → World 构造、AddModule、Update 生命周期
    IModuleTests.cs          → 模块挂载 + System 分区
    GameTimeTests.cs
    EventBusTests.cs         → 双缓冲 + 订阅分发 + EntityDeathEvent
    DeterministicRngTests.cs → 确定性（同 seed 同序列）、Fork 独立性
    PrefabTests.cs           → 组件模板 + 实例化
    EntitySnapshotTests.cs   → 组件序列化往返
    MovementSystemTests.cs
    TimerSystemTests.cs
    SpawnSystemTests.cs
    HealthSystemTests.cs
    LifetimeSystemTests.cs
```

**独立运行验证**（同日证明「Core 不带 GAS 可独立跑」）：单测里建 `World(ENetMode.Standalone).AddModule<MovementModule>()`，手动 `Update` N 帧断言位置变化。

## 16. v1 范围边界

**做**：
- 迁 `World` + `ENetMode` 到 `Gameplay.Core`，补引用 using
- 实现 `IModule` + `World`（AddModule/AddSystem/AddService/Update）
- 实现 `GameTime`（Variable 步长；Fixed 提供接口）
- 实现 `ESimulationStage` 调度
- 实现 `EntityLifecycle` 钩子封装
- 实现 `EventBus`（通用事件总线，含 `EntityDeathEvent`）
- 实现 `DeterministicRng`
- 实现 `Prefab` + `PrefabRegistry`
- 实现 `IComponentSerializer` + `ByteWriter`/`ByteReader` + `SerializerRegistry` + `EntitySnapshot`
- 实现通用玩法组件 + 系统（Transform/Movement、Team/PlayerState/Owner、Spawn/Timer、Health/Death、Lifetime）
- 补齐 `Gameplay.Tests.Core` 单测

**不做（Phase 2 或后续）**：
- `GameplayAbilitiesFeature` 重构为 `IModule`
- `ETimeStep.Fixed` 完整实现（v1 仅接口）
- 序列化 CodeGen 自动生成
- 状态同步（Bubble/预测回滚）——Core 之上的独立模块
- 对象池、GameMode/GameState、RPC、Console
- `Gameplay.Tasks` 通用系统的清理/去重
- samples 的 Core.Demo 可执行示例

## 17. 依赖方向

```
Gameplay.Abilities ──┐
Gameplay.Tasks ──────┤── 消费（单向依赖）──► Gameplay.Core ──► Friflo.Engine.ECS
Gameplay.Tags ───────┘
Gameplay.Interfaces   （与 Core 平行，IInputService 是独立桥梁）
```

- Core **不引用** Abilities/Tasks/Tags 的任何类型（依赖方向单向）
- 允许：「能力消费 Core 服务」；禁止：「Core 依赖业务规则」（沿用 CLAUDE.md 依赖方向原则）