# Gameplay.Replication 设计文档（状态同步 v1）

日期：2026-08-20
状态：待评审

## 1. 概述

`Gameplay.Replication` 是 Gameplay.NET 的**状态同步模块**——服务端权威 + 客户端镜像的单向复制（server-authoritative replication）。它建立在 `Gameplay.Core` 之上，解决「服务端改组件 → 客户端同步更新」的核心问题，并通过 **Bubble 可见性**管理「哪些实体同步给哪个客户端」。

**定位**：
- 是 `Gameplay.Core` 之上的**独立模块**（`Gameplay.dll` 内的 namespace `Gameplay.Replication`），Core 不含网络
- **纯同步逻辑**：复制协议、Bubble 管理、权威复制、客户端镜像、变更检测——**不含 socket 传输**
- 传输通过 `IReplicationTransport` 接口注入，真正的网络传输在 `Gameplay.Infrastructure`（或后续样本）实现
- **v1 只做服务端权威单向复制**（含 Bubble）；客户端预测/回滚是**后续 spec**，不在本 spec 范围

**核心目标**：服务端是唯一真相源，客户端持有只读镜像副本；服务端按 Bubble 把权威 Component 状态增量同步给各客户端。

## 2. 关键决策

| 决策点 | 结论 |
|--------|------|
| 模块归属 | `Gameplay.dll` 内 `namespace Gameplay.Replication`，纯逻辑 + `IReplicationTransport` 注入（不碰 socket） |
| 首个 spec 范围 | 复制协议 + 服务端权威单向复制（含 Bubble 可见性）；**预测回滚是后续 spec** |
| 复制集标记 | `[Replicated]` 特性（`Gameplay.Shared`）+ 源生成器 `ReplicationGenerator`（`Gameplay.CodeGen`） |
| 组件序列化 | 复用现有 `SerializerRegistry`（组件手写 `IComponentSerializer<T>`） |
| 实体跨进程身份 | **外部映射表** `NetworkId`——身份在 packet 信封、状态在 payload，`Dictionary<NetworkId, Entity>` 由复制层维护（对齐 UE5 NetGUID） |
| 同步粒度 | **增量 delta（shadow-diff）+ 全量快照兜底** |
| dirty 判定 | **字段级 `Equals(in T, in T)`**（SG 自动生成），dirty 发送粒度 = **整个组件** |
| Bubble 相关性 | **规则驱动 Owner-based**：无 `OwnerComponent` 或 `PlayerId == -1` → 广播；有 owner → 仅 owner 的 Bubble |
| 模块挂载 | `ReplicationModule : IModule`（构造注入 `World` + transport），按 `World.NetMode` 挂服务端/客户端 |

### 2.1 服务端权威声明

服务端是**唯一可写复制状态的权威端**。v1 客户端是**被动镜像**——只接收并应用服务端下发的状态，不主动修改复制组件、不上送输入（客户端输入走 RPC 上送 + 预测是后续 spec）。预测/回滚由后续 spec 复用本模块的协议层实现。

## 3. 目录结构与命名空间

```
src/Gameplay/Gameplay.Replication/            → namespace Gameplay.Replication
├── NetworkId.cs                       → 网络身份（struct）
├── IReplicationTransport.cs           → 传输接口
├── IReplicationDiff.cs                → 组件相等判定接口
├── ReplicationRegistry.cs             → 复制集注册（含 IReplicationEntry / ReplicationEntry<T>）
├── ReplicationPacket.cs               → 包类型枚举 + 编解码
├── ReplicationDelta.cs                → 内部 dirty 增量结构
├── ReplicationServer.cs               → 服务端权威（Bubble + NetworkId 分配）
├── ReplicationClient.cs               → 客户端镜像（NetworkId → Entity 映射）
├── ReplicationSystem.cs               → 服务端每帧 System（shadow-diff + 发送）
├── ReplicationClientSystem.cs         → 客户端每帧 System（接收 + 应用）
└── ReplicationModule.cs               → IModule（按 NetMode 挂载）

src/Gameplay.Shared/ReplicatedAttribute.cs   → [Replicated] 标记特性
src/Gameplay.CodeGen/ReplicationGenerator.cs → SG：扫描 [Replicated] 生成 diff + RegisterAll
```

文件范围命名空间随目录走（`namespace Gameplay.Replication;`），枚举以 `E` 打头。

## 4. 复制协议

### 4.1 复制集标记：`[Replicated]` + 源生成器

`Gameplay.Shared` 新增标记特性：

```csharp
namespace Gameplay.Replication;

/// <summary>标记 struct 组件参与网络复制。SG 扫描生成 field-wise Equals + RegisterAll。</summary>
[AttributeUsage(AttributeTargets.Struct)]
public class ReplicatedAttribute : System.Attribute { }
```

`Gameplay.CodeGen` 新增 `ReplicationGenerator : IIncrementalGenerator`，扫描带 `[Replicated]` 的 struct，对每个组件类型生成：

1. **`readonly struct XxxReplication : IReplicationDiff<Xxx>`**——field-wise `Equals(in Xxx a, in Xxx b)`，逐字段 `==` 比较（`bool`/`int`/`enum`/`float` 用 `==`；`Vector3`/`Quaternion` 用其 `IEquatable<T>.Equals`）。v1 用精确比较（`0.0f == -0.0f` 为 true、`NaN != NaN` 为 true，语义正确）；**float 容差留作后续增强**。
2. **`ReplicatedComponentRegistration.RegisterAll(ReplicationRegistry)`**——逐个 `registry.Register<Xxx>(new XxxReplication())`。

生成器遵循现有 `GameplayEventGenerator` 的模式：用 `compilation` 判断「只在定义 `ReplicationRegistry` 的当前程序集生成 `RegisterAll`」（`HasReplicationRegistry(compilation)`），避免在引用程序集重复生成。

**启动注册流**：`ReplicatedComponentRegistration.RegisterAll(ReplicationRegistry)` 由使用方在启动时（World 创建后、首次 `Update` 前）调用一次，把全部 `[Replicated]` 组件装配进复制集。

**约束**：v1 复制组件字段只能是 primitive / `Vector3` / `Quaternion` / `enum`——**不含 `Entity` 类型字段**（跨实体引用翻译留后续）。

### 4.2 复制条目：`ReplicationRegistry`

```csharp
public static class ReplicationRegistry
{
    /// <summary>注册复制组件——装配「SerializerRegistry 的序列化器 + SG 生成的 diff」。</summary>
    public static void Register<T>(IReplicationDiff<T> diff) where T : struct, IComponent;

    internal static IReplicationEntry? GetEntry(int typeId);
    internal static IReadOnlyList<IReplicationEntry> Entries { get; }
}
```

- `Register<T>` 从 `SerializerRegistry.Get<T>()` 取序列化器（未注册则抛异常 fail-fast），`typeId` 复用 `SerializerRegistry.ComputeTypeId(typeof(T))`（FNV-1a，跨进程稳定、与 `EntitySnapshot` 一致），装配成 `ReplicationEntry<T>`。
- `IReplicationEntry`（非泛型接口）+ `ReplicationEntry<T>`（泛型适配器）暴露：`TypeId` / `HasComponent` / `Capture`（写全量）/ `Apply`（读回并应用）/ `CreateShadowStore()`（每 World 新建 shadow 状态）。
- **类型级 vs 实例级分离**（对齐 Core「类型级注册 static、实例级状态挂 World」原则）：`ReplicationRegistry` 与 `ReplicationEntry<T>` 是 `static` 类型级注册（serializer + diff，无状态、跨 World 共享）；**shadow 状态是 per-World 实例**，由 `ReplicationServer` 持有，多 World 互不污染。

### 4.3 实体身份：`NetworkId`

```csharp
public readonly struct NetworkId
{
    public readonly int Value;
    public NetworkId(int value) => Value = value;
    public bool IsValid => Value > 0;
    public static NetworkId Invalid => default;
}
```

- 服务端分配**自增正数** NetworkId（从 1 起），每个复制实体唯一。
- 身份**不在组件里**（对齐 UE5 NetGUID）：服务端 `ReplicationServer` 持 `Dictionary<int entityId, NetworkId>`，客户端 `ReplicationClient` 持 `Dictionary<NetworkId, Entity>`。
- **身份在 packet 信封、状态在 payload**——packet 头带 NetworkId，payload 带组件序列化数据，互不污染。

### 4.4 变更检测：shadow-diff

shadow 状态是 **per-World 实例**：每个 `ReplicationServer` 持 `Dictionary<int typeId, IShadowStore>`，其中 `ShadowStore<T>`（经 `ReplicationEntry<T>.CreateShadowStore()` 创建）持有 `Dictionary<int entityId, T>`（每个复制实体上次已发送值的拷贝）。每帧：

1. `ReplicationEntry<T>.Diff(ShadowStore<T> shadows, EntityStore store, List<ReplicationDelta> output)` 遍历 `store.Query<T>()`，对每个实体：`shadows.TryGetValue(entity.Id, out var shadow)`：
   - **有 shadow**：`!diff.Equals(current, shadow)` → 产出 dirty `(entity, typeId)`，并更新 shadow；
   - **无 shadow**（新实体 / 新挂复制组件）→ 产出 dirty（首帧触发 spawn），并写入 shadow。
2. 只序列化 dirty 组件（稳态零序列化、零 GC）；发送粒度 = 整个组件。
3. 实体删除：清理该实体的 shadow（并触发 despawn）。

### 4.5 包格式

单实体操作，type 判别（`EReplicationPacketType`）：

```
[byte Type][payload]

ESpawn(1)        NetworkId(int) + [count(int)][typeId(int)+data]*   // 组件全量（复用 EntitySnapshot 的 [count][typeId+data]* 头）
EUpdate(2)       NetworkId(int) + [count(int)][typeId(int)+data]*   // 只 dirty 组件
EDespawn(3)      NetworkId(int)
EFullSnapshot(4) [count(int)]{ NetworkId(int) + [count(int)][typeId(int)+data]* }*   // 某 Bubble 全量
```

- `typeId` 复用 `SerializerRegistry` 的 FNV-1a typeId，客户端按 `ReplicationRegistry.GetEntry(typeId)` 查条目应用。
- 编解码走 `ByteWriter`/`ByteReader`（复用 Core 序列化底层）。
- **批量打包（多实体合并一条消息）是后续优化**，v1 一条消息一个实体操作。

### 4.6 传输接口：`IReplicationTransport`

```csharp
public interface IReplicationTransport
{
    // 服务端侧
    IReadOnlyList<int> ClientIds { get; }
    void SendToClient(int clientId, ReadOnlySpan<byte> payload);
    // 客户端侧
    bool TryReceiveFromServer(out ReadOnlySpan<byte> payload);
    // 预留（v1 客户端→服务端单向，不上送）
    void SendToServer(ReadOnlySpan<byte> payload);
    bool TryReceiveFromClient(int clientId, out ReadOnlySpan<byte> payload);
}
```

- **消息导向、拉模型**：`ReplicationClientSystem` 每帧 `TryReceiveFromServer` 轮询；服务端 `SendToClient` 主动发送。
- `ReadOnlySpan<byte>` 由 transport 内部缓冲持有（`byte[]`/`ArrayPool`），有效到下一次接收。
- 单测用 `LoopbackReplicationTransport`（内存实现，N 客户端 + 可选模拟延迟）。
- **v1 假定 `clientId == PlayerId`**（连接与玩家 1:1），真正的连接↔玩家映射是 Infrastructure 职责。

## 5. 服务端权威

### 5.1 `ReplicationServer`（POCO 服务）

```csharp
public sealed class ReplicationServer
{
    public ReplicationServer(IReplicationTransport transport);

    // 客户端状态（每客户端一个 Bubble）
    private readonly Dictionary<int, ClientState> clients;  // clientId → ClientState

    // NetworkId 分配
    private int nextNetworkId = 1;
    private readonly Dictionary<int, NetworkId> entityToNetId;  // entityId → NetworkId

    public NetworkId GetNetworkId(int entityId);
    public void AddClient(int clientId);       // 新客户端加入（触发全量快照）
    public void RemoveClient(int clientId);
    public void Tick(...);                      // 由 ReplicationSystem 驱动
}
```

### 5.2 Bubble 可见性（Owner-based relevancy）

每个客户端一个 `Bubble = HashSet<NetworkId>`。实体首次挂上复制组件时（`EntityLifecycle.ComponentAdded`），按**规则**决定进哪些 Bubble：

- 无 `OwnerComponent` 或 `OwnerComponent.PlayerId == -1` → **广播**进所有客户端的 Bubble；
- 有 owner → **仅进 owner 对应客户端**的 Bubble（`clientId == PlayerId`）。

`ClientState` 含 `NeedsSnapshot` 标记：新连接（`AddClient`）或周期性触发时置位，下一帧发 `EFullSnapshot` 兜底防漂移。

### 5.3 NetworkId 分配 + spawn/despawn（经 EntityLifecycle）

`ReplicationServer` 订阅 `EntityLifecycle`：

- **ComponentAdded**（组件是复制组件）：实体尚未分配 NetworkId → 分配自增 NetworkId，登记映射，按 Owner 规则加入 Bubble（触发 spawn）。
- **EntityDeleted**：从 `entityToNetId` 取 NetworkId → 从各 Bubble 移除 → 广播 `EDespawn`。

> 复制组件中途移除（replicated component removal）是 v1 已知边界：复制组件通常实体生命周期内稳定，移除语义留后续（包格式可加 `ComponentRemoved` 标记扩展）。

### 5.4 `ReplicationSystem`（服务端每帧 System）

挂 `PostSimulation`（在 Simulation 全部 System 改完组件之后跑，保证当帧变更被捕获）：

1. 对每个注册的复制组件类型 `entry.Diff(store, deltas)` 收集 dirty `(entity, typeId)`；
2. 按实体聚合：实体 NetworkId 是否已在目标客户端 Bubble 中——不在（新进 Bubble / 新连接）→ 发 `ESpawn`；在 → 发 `EUpdate`（含 dirty 组件）；
3. 每个 Bubble 按 `NeedsSnapshot` 触发 `EFullSnapshot`；
4. 经 transport `SendToClient` 发送。

## 6. 客户端镜像

### 6.1 `ReplicationClient`

```csharp
public sealed class ReplicationClient
{
    public ReplicationClient(EntityStore store, IReplicationTransport transport);
    private readonly Dictionary<NetworkId, Entity> mirror;  // NetworkId → 本地镜像实体
}
```

### 6.2 `ReplicationClientSystem`（客户端每帧 System）

挂 `PreSimulation`（在 Simulation 之前应用服务端状态，保证本帧模拟基于最新权威状态）：

1. 轮询 `transport.TryReceiveFromServer`，按 `EReplicationPacketType` 分发：
   - `ESpawn`：创建镜像实体 → 按 `[count][typeId+data]*` 应用组件 → 登记 `mirror[NetworkId] = entity`；
   - `EUpdate`：`mirror` 查实体 → 应用 dirty 组件；
   - `EDespawn`：`mirror` 查实体 → 删除镜像 → 移除映射；
   - `EFullSnapshot`：对齐——创建缺失镜像、应用组件、删除本地多余镜像。

## 7. `ReplicationModule` 与 Host/Standalone

```csharp
public sealed class ReplicationModule : IModule
{
    public ReplicationServer? Server { get; }
    public ReplicationClient? Client { get; }

    public ReplicationModule(World world, IReplicationTransport transport)
    {
        // 按 World.NetMode + 编译宏挂载
        // DedicatedServer / ListenServer：挂 ReplicationServer + ReplicationSystem
        // Client / ListenServer：       挂 ReplicationClient + ReplicationClientSystem
        // Standalone：                  不挂任何（零网络）
    }
}
```

- 服务端类型以 `#if GP_WITH_SERVER_CODE` 包裹，客户端类型以 `#if !GP_SERVER` 包裹（对齐 `GameplayAbilitiesModule` 的 `CreateCueManager` 模式）。
- 运行时 `world.NetMode` 决定挂载：`Standalone` 不挂；`DedicatedServer` 只挂服务端；`Client` 只挂客户端；`ListenServer`（Host）两者都挂。
- **Host 模式**：本地客户端走 **in-process `LoopbackReplicationTransport`**（与真实客户端同一条代码路径，`NetworkId`/镜像逻辑完全复用），保证 Host 路径被测试覆盖。

## 8. 依赖方向

```
Gameplay.Replication ──► Gameplay.Core ──► Friflo.Engine.ECS
        │
        └──► IReplicationTransport（注入，传输在 Infrastructure / 样本）
```

- `Gameplay.Replication` 消费 `Gameplay.Core` 的 `World` / `EntitySnapshot` / `SerializerRegistry` / `ENetMode` / `EntityLifecycle` / `ByteWriter`/`ByteReader`，单向。
- `Gameplay.Replication` 不依赖任何 socket / 具体传输实现（依赖倒置：传输经接口注入）。
- `Gameplay.Core` 不引用 `Gameplay.Replication`（Core 不含网络）。

## 9. 测试与成功标准

测试目录 `tests/Gameplay.Tests/Gameplay.Tests.Sync/`，用 `LoopbackReplicationTransport` 建「一个 `DedicatedServer` 权威 World + N 个 `Client` 镜像 World」：

| 测试文件 | 覆盖 |
|----------|------|
| `ReplicationRegistryTests.cs` | `Register<T>`、序列化器缺失 fail-fast、typeId 一致 |
| `ReplicationDiffTests.cs` | SG 生成 Equals 的 field-wise 正确性（改字段→不等、未改→等） |
| `ReplicationServerTests.cs` | NetworkId 分配、Owner-based Bubble 相关性、spawn/despawn |
| `ReplicationClientTests.cs` | 镜像创建/应用/删除、NetworkId→Entity 映射 |
| `ReplicationSyncTests.cs` | 端到端：服务端改组件 → 客户端镜像同步；dirty 只发变化组件；全量快照兜底 |
| `ReplicationVisibilityTests.cs` | 不同客户端看到不同实体集（Owner-based） |
| `LoopbackTransportTests.cs` | 消息往返、多客户端、可选延迟 |

**成功标准**：
- `dotnet test` 全绿；
- 三个 `GameplayMode`（`Client`/`Host`/`Server`）都编译通过；
- 端到端测试证明「服务端改组件 → 客户端镜像同步更新」闭环成立。

## 10. v1 范围边界

**做**：
- `[Replicated]` 特性 + `ReplicationGenerator` SG
- `ReplicationRegistry` + `IReplicationDiff<T>` + `ReplicationEntry<T>`
- `NetworkId` + `IReplicationTransport` + `LoopbackReplicationTransport`
- `ReplicationServer`（Bubble + NetworkId 分配 + spawn/despawn）
- `ReplicationClient`（镜像 + 映射）
- `ReplicationSystem` / `ReplicationClientSystem`（shadow-diff + 收发）
- `ReplicationModule`（按 NetMode 挂载）
- `ReplicationPacket`（Spawn/Update/Despawn/FullSnapshot 编解码）
- 端到端 loopback 单测

**不做（后续 spec）**：
- 客户端预测 + 回滚（reconciliation）
- 客户端→服务端输入 / RPC 上送
- 插值（表现层视觉平滑）
- 跨实体引用（`Entity` 字段）的 `Entity → NetworkId` 翻译
- 字段级打包、多实体批量、压缩、带宽优化
- `GameState`/`GameMode` 下推、对象池、真实 socket 传输
