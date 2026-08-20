# Gameplay.Replication Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 构建 `Gameplay.Replication` —— 服务端权威 + 客户端镜像的单向状态复制（`[Replicated]` + SG、NetworkId 外部映射、shadow-diff 增量、Bubble 可见性、loopback 端到端）。

**Architecture:** `Gameplay.dll` 内 namespace `Gameplay.Replication`；`[Replicated]` 标记 + 源生成器产出 serializer/diff/RegisterAll 三件套；服务端 `ReplicationServer` 维护 per-client 双集合（`Bubble`/`Mirrored`）做 shadow-diff 增量；客户端 `ReplicationClient` 纯镜像；传输经 `IReplicationServerTransport`/`IReplicationClientTransport` 注入，loopback 单测。

**Tech Stack:** .NET（`netstandard2.1` + `net10.0`）、Friflo.Engine.ECS 3.x、Roslyn 源生成器、xUnit。

**Spec:** `docs/superpowers/specs/2026-08-20-replication-design.md`

## Global Constraints

- 文档/注释用**中文**，专业术语英文；枚举以 `E` 打头（`EReplicationPacketType`）；枚举文件名不带 `E`（`ReplicationPacket.cs`）。
- 私有字段 camelCase 无下划线前缀（`store` 而非 `_store`）；公开成员 PascalCase。
- 文件范围命名空间：`src/Gameplay/Gameplay.Replication/` 下用 `namespace Gameplay.Replication;`；`[Replicated]` 特性在 `src/Gameplay.Shared/` 用 `namespace Gameplay;`（中性，避免 Core→Replication 依赖）；测试 `tests/Gameplay.Tests/Gameplay.Tests.Replication/` 用 `namespace Gameplay.Tests.Replication;`。
- **Friflo IComponent 修改必须走 ref**：`entity.GetComponent<T>()` 返回 `ref`，`TryGetComponent<T>(out var)` 是值拷贝陷阱。
- **`QuerySystem.Query` 只在 `OnUpdate` 内有效**；`ReplicationSystem`/`ReplicationClientSystem` 是 `BaseSystem`（非 `QuerySystem<T>`），内部用 `store.Query<T>()`/`store.GetEntityById` 遍历。
- 0 GC：热路径（`Diff` 每帧遍历、`ReplicationClientSystem` 解码）严格——`ShadowStore<T>` 用 `Dictionary` 是 v1 接受的实现（dirty 才更新 shadow，稳态不分配）；冷路径（注册、序列化）可放松。
- 组件为纯数据 struct；System 持有全部逻辑。
- 类型级 vs 实例级分离：`SerializerRegistry`/`ReplicationRegistry` 是 `static` 类型级注册；shadow 状态、`clients` 是 per-World 实例（挂 `ReplicationServer`）。
- 测试命令：`dotnet test tests/Gameplay.Tests/Gameplay.Tests.csproj -f net10.0 --filter "FullyQualifiedName~ClassName"`；构建：`dotnet build src/Gameplay/Gameplay.csproj`。

---

## File Structure

```
src/Gameplay.Shared/ReplicatedAttribute.cs          → [Replicated] 标记（namespace Gameplay）
src/Gameplay.CodeGen/ReplicationGenerator.cs        → SG：serializer + diff + RegisterAll

src/Gameplay/Gameplay.Replication/                  → namespace Gameplay.Replication
├── NetworkId.cs                                     → 网络身份 struct
├── IReplicationDiff.cs                              → 组件相等判定接口
├── IReplicationServerTransport.cs                   → 服务端传输接口
├── IReplicationClientTransport.cs                   → 客户端传输接口
├── ReplicationRegistry.cs                           → 复制集注册 + IReplicationEntry + ReplicationEntry<T> + ShadowStore<T>
├── ReplicationDelta.cs                              → 内部 dirty 增量 struct
├── ReplicationPacket.cs                             → EReplicationPacketType + 编解码
├── ReplicationServer.cs                             → 服务端权威（双集合 + NetworkId + 生命周期）
├── ReplicationClient.cs                             → 客户端镜像（映射）
├── ReplicationSystem.cs                             → 服务端每帧 System
├── ReplicationClientSystem.cs                       → 客户端每帧 System
└── ReplicationModule.cs                             → IModule

tests/Gameplay.Tests/Gameplay.Tests.Replication/     → namespace Gameplay.Tests.Replication
├── SyncTestComponent.cs                             → 测试组件 + 手写 serializer/diff（Task 2-8 用）
├── NetworkIdTests.cs
├── ReplicationRegistryTests.cs
├── ReplicationPacketTests.cs
├── ReplicationServerTests.cs
├── ReplicationClientTests.cs
├── LoopbackReplicationTransport.cs                  → 内存传输实现
├── ReplicationSyncTests.cs                          → 端到端
└── ReplicationGeneratorTests.cs                     → SG 生成代码（Task 9）
```

> 任务 2-8 用测试项目里的 `SyncTestComponent`（手写 serializer+diff）验证复制逻辑；任务 9 加入 SG 后标记真实 Core 组件（`Transform`/`Velocity`/`Health`/`Owner`）并用生成代码端到端验证。

---

### Task 1: 复制契约基础类型（NetworkId / IReplicationDiff / 传输接口）

**Files:**
- Create: `src/Gameplay/Gameplay.Replication/NetworkId.cs`
- Create: `src/Gameplay/Gameplay.Replication/IReplicationDiff.cs`
- Create: `src/Gameplay/Gameplay.Replication/IReplicationServerTransport.cs`
- Create: `src/Gameplay/Gameplay.Replication/IReplicationClientTransport.cs`
- Test: `tests/Gameplay.Tests/Gameplay.Tests.Replication/NetworkIdTests.cs`

**Interfaces:**
- Consumes: 无（叶子类型）
- Produces:
  - `NetworkId`（`readonly struct`：`int Value`、`bool IsValid`、`static NetworkId Invalid`）
  - `IReplicationDiff<T> where T : struct, IComponent`（`bool Equals(in T a, in T b)`）
  - `IReplicationServerTransport`（`void SendToClient(int clientId, ReadOnlySpan<byte> payload)`、`bool TryReceiveFromClient(int clientId, out ReadOnlySpan<byte> payload)`）
  - `IReplicationClientTransport`（`bool TryReceiveFromServer(out ReadOnlySpan<byte> payload)`、`void SendToServer(ReadOnlySpan<byte> payload)`）

- [ ] **Step 1: 写 NetworkId 失败测试**

`tests/Gameplay.Tests/Gameplay.Tests.Replication/NetworkIdTests.cs`：

```csharp
using Gameplay.Replication;
using Xunit;

namespace Gameplay.Tests.Replication;

public class NetworkIdTests
{
    [Fact]
    public void Default_IsInvalid()
    {
        Assert.False(default(NetworkId).IsValid);
    }

    [Fact]
    public void PositiveValue_IsValid()
    {
        Assert.True(new NetworkId(1).IsValid);
        Assert.Equal(42, new NetworkId(42).Value);
    }
}
```

- [ ] **Step 2: 运行确认失败**

Run: `dotnet test tests/Gameplay.Tests/Gameplay.Tests.csproj -f net10.0 --filter "FullyQualifiedName~NetworkIdTests"`
Expected: 编译错误（`NetworkId` 未定义）。

- [ ] **Step 3: 实现 4 个叶子类型**

`src/Gameplay/Gameplay.Replication/NetworkId.cs`：

```csharp
namespace Gameplay.Replication;

/// <summary>跨进程实体网络身份（服务端分配自增正数，0 = Invalid）。</summary>
public readonly struct NetworkId
{
    public readonly int Value;

    public NetworkId(int value) => Value = value;

    /// <summary>是否有效（正数）。</summary>
    public bool IsValid => Value > 0;

    public static NetworkId Invalid => default;
}
```

`src/Gameplay/Gameplay.Replication/IReplicationDiff.cs`：

```csharp
using Friflo.Engine.ECS;

namespace Gameplay.Replication;

/// <summary>组件相等判定（shadow-diff 用）——字段级比较。</summary>
public interface IReplicationDiff<T> where T : struct, IComponent
{
    bool Equals(in T a, in T b);
}
```

`src/Gameplay/Gameplay.Replication/IReplicationServerTransport.cs`：

```csharp
using System;
using System.Collections.Generic;

namespace Gameplay.Replication;

/// <summary>服务端传输（纯消息管道，不持有客户端集合）。</summary>
public interface IReplicationServerTransport
{
    void SendToClient(int clientId, ReadOnlySpan<byte> payload);
    bool TryReceiveFromClient(int clientId, out ReadOnlySpan<byte> payload); // v1 预留
}
```

`src/Gameplay/Gameplay.Replication/IReplicationClientTransport.cs`：

```csharp
using System;

namespace Gameplay.Replication;

/// <summary>客户端传输。</summary>
public interface IReplicationClientTransport
{
    bool TryReceiveFromServer(out ReadOnlySpan<byte> payload);
    void SendToServer(ReadOnlySpan<byte> payload); // v1 预留
}
```

- [ ] **Step 4: 运行通过**

Run: `dotnet test tests/Gameplay.Tests/Gameplay.Tests.csproj -f net10.0 --filter "FullyQualifiedName~NetworkIdTests"`
Expected: 全部通过。

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "添加 Gameplay.Replication 基础类型（NetworkId/IReplicationDiff/传输接口）"
```

---

### Task 2: ReplicationRegistry + ReplicationEntry + ShadowStore + ReplicationDelta

**Files:**
- Create: `src/Gameplay/Gameplay.Replication/ReplicationDelta.cs`
- Create: `src/Gameplay/Gameplay.Replication/ReplicationRegistry.cs`
- Test: `tests/Gameplay.Tests/Gameplay.Tests.Replication/SyncTestComponent.cs`
- Test: `tests/Gameplay.Tests/Gameplay.Tests.Replication/ReplicationRegistryTests.cs`

**Interfaces:**
- Consumes: `IReplicationDiff<T>`、`IComponentSerializer<T>`（Core 已有）、`SerializerRegistry`（Core 已有，`Get<T>()`/`ComputeTypeId`）、Friflo `ComponentType`/`EntityStore`
- Produces:
  - `ReplicationDelta`（`readonly struct`：`Entity Entity`、`int TypeId`）
  - `ReplicationRegistry`（`public static void Register<T>(IReplicationDiff<T> diff)`、`internal static IReplicationEntry? GetEntry(int typeId)`、`internal static IReplicationEntry? GetByComponentType(ComponentType type)`、`internal static IReadOnlyList<IReplicationEntry> Entries`）
  - `IReplicationEntry`（internal，见下方代码）与 `ReplicationEntry<T>`（internal）

- [ ] **Step 1: 写测试组件 + 失败测试**

`tests/Gameplay.Tests/Gameplay.Tests.Replication/SyncTestComponent.cs`：

```csharp
using Friflo.Engine.ECS;
using Gameplay.Core;
using Gameplay.Replication;

namespace Gameplay.Tests.Replication;

/// <summary>复制逻辑测试用的组件（手写 serializer/diff，Task 9 前替代 SG 生成）。</summary>
public struct SyncTestComponent : IComponent
{
    public int Value;
}

/// <summary>SyncTestComponent 手写序列化器。</summary>
public sealed class SyncTestSerializer : IComponentSerializer<SyncTestComponent>
{
    public void Write(in SyncTestComponent c, ref ByteWriter w) => w.Write(c.Value);
    public void Read(ref SyncTestComponent c, ref ByteReader r) => c.Value = r.ReadInt();
}

/// <summary>SyncTestComponent 手写 diff。</summary>
public readonly struct SyncTestDiff : IReplicationDiff<SyncTestComponent>
{
    public bool Equals(in SyncTestComponent a, in SyncTestComponent b) => a.Value == b.Value;
}
```

`tests/Gameplay.Tests/Gameplay.Tests.Replication/ReplicationRegistryTests.cs`：

```csharp
using Friflo.Engine.ECS;
using Gameplay.Core;
using Gameplay.Replication;
using Xunit;

namespace Gameplay.Tests.Replication;

public class ReplicationRegistryTests
{
    [Fact]
    public void Register_MissingSerializer_Throws()
    {
        // 未注册 serializer 直接 Register diff → fail-fast
        Assert.Throws<InvalidOperationException>(
            () => ReplicationRegistry.Register<SyncTestComponent>(new SyncTestDiff()));
    }

    [Fact]
    public void Register_ThenCaptureApply_Roundtrips()
    {
        SerializerRegistry.Register(new SyncTestSerializer());
        ReplicationRegistry.Register<SyncTestComponent>(new SyncTestDiff());

        var store = new EntityStore();
        var entity = store.CreateEntity();
        entity.AddComponent(new SyncTestComponent { Value = 77 });

        var entry = ReplicationRegistry.GetByComponentType(ComponentType<SyncTestComponent>.Value);
        Assert.NotNull(entry);

        Span<byte> buf = stackalloc byte[64];
        var writer = new ByteWriter(buf);
        entry!.Capture(entity, ref writer);
        // 修改原组件
        ref var comp = ref entity.GetComponent<SyncTestComponent>();
        comp.Value = 1;

        var reader = new ByteReader(buf[..writer.BytesWritten]);
        entry.Apply(entity, ref reader);

        Assert.Equal(77, entity.GetComponent<SyncTestComponent>().Value);
    }
}
```

- [ ] **Step 2: 运行确认失败**

Run: `dotnet test tests/Gameplay.Tests/Gameplay.Tests.csproj -f net10.0 --filter "FullyQualifiedName~ReplicationRegistryTests"`
Expected: 编译错误（`ReplicationRegistry`/`ReplicationDelta` 未定义）。

- [ ] **Step 3: 实现 ReplicationDelta + ReplicationRegistry**

`src/Gameplay/Gameplay.Replication/ReplicationDelta.cs`：

```csharp
using Friflo.Engine.ECS;

namespace Gameplay.Replication;

/// <summary>shadow-diff 产出的 dirty 增量（实体 + 组件 typeId）。</summary>
internal readonly struct ReplicationDelta
{
    public readonly Entity Entity;
    public readonly int TypeId;

    public ReplicationDelta(Entity entity, int typeId)
    {
        Entity = entity;
        TypeId = typeId;
    }
}
```

`src/Gameplay/Gameplay.Replication/ReplicationRegistry.cs`：

```csharp
using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Gameplay.Core;

namespace Gameplay.Replication;

/// <summary>复制集注册中心（static，类型级，跨 World 共享）——装配「序列化器 + diff」。</summary>
public static class ReplicationRegistry
{
    private static readonly Dictionary<int, IReplicationEntry> byTypeId = new();
    private static readonly Dictionary<ComponentType, IReplicationEntry> byComponentType = new();
    private static readonly List<IReplicationEntry> entries = new();

    /// <summary>注册复制组件（须先在 SerializerRegistry 注册序列化器，否则 fail-fast）。</summary>
    public static void Register<T>(IReplicationDiff<T> diff) where T : struct, IComponent
    {
        var type = typeof(T);
        var serializer = SerializerRegistry.Get<T>()
            ?? throw new InvalidOperationException($"组件 {type.FullName} 未注册序列化器，无法复制（先 SerializerRegistry.Register）");
        int typeId = SerializerRegistry.ComputeTypeId(type);
        var entry = new ReplicationEntry<T>(typeId, ComponentType<T>.Value, serializer, diff);
        byTypeId[typeId] = entry;
        byComponentType[entry.ComponentType] = entry;
        entries.Add(entry);
    }

    internal static IReplicationEntry? GetEntry(int typeId)
        => byTypeId.TryGetValue(typeId, out var e) ? e : null;

    internal static IReplicationEntry? GetByComponentType(ComponentType type)
        => byComponentType.TryGetValue(type, out var e) ? e : null;

    internal static IReadOnlyList<IReplicationEntry> Entries => entries;
}

/// <summary>非泛型复制条目（EntitySnapshot 式统一编解码 + shadow-diff）。</summary>
internal interface IReplicationEntry
{
    int TypeId { get; }
    ComponentType ComponentType { get; }
    bool HasComponent(Entity entity);
    void Capture(Entity entity, ref ByteWriter writer);   // 只写组件数据（不含 typeId）
    void Apply(Entity entity, ref ByteReader reader);      // 只读组件数据
    IShadowStore CreateShadowStore();
    void Diff(IShadowStore shadowStore, EntityStore store, List<ReplicationDelta> output);
}

/// <summary>shadow 状态（per-World 实例）。</summary>
internal interface IShadowStore { }

internal sealed class ShadowStore<T> : IShadowStore where T : struct, IComponent
{
    public readonly Dictionary<int, T> ByEntityId = new();
}

/// <summary>泛型适配器——IComponentSerializer&lt;T&gt; + IReplicationDiff&lt;T&gt; → IReplicationEntry。</summary>
internal sealed class ReplicationEntry<T> : IReplicationEntry where T : struct, IComponent
{
    private readonly IComponentSerializer<T> serializer;
    private readonly IReplicationDiff<T> diff;

    public int TypeId { get; }
    public ComponentType ComponentType { get; }

    public ReplicationEntry(int typeId, ComponentType componentType, IComponentSerializer<T> serializer, IReplicationDiff<T> diff)
    {
        TypeId = typeId;
        ComponentType = componentType;
        this.serializer = serializer;
        this.diff = diff;
    }

    public bool HasComponent(Entity entity) => entity.HasComponent<T>();

    public void Capture(Entity entity, ref ByteWriter writer)
    {
        ref var c = ref entity.GetComponent<T>();
        serializer.Write(in c, ref writer);
    }

    public void Apply(Entity entity, ref ByteReader reader)
    {
        if (!entity.HasComponent<T>())
            entity.AddComponent<T>();
        ref var c = ref entity.GetComponent<T>();
        serializer.Read(ref c, ref reader);
    }

    public IShadowStore CreateShadowStore() => new ShadowStore<T>();

    public void Diff(IShadowStore shadowStore, EntityStore store, List<ReplicationDelta> output)
    {
        var shadows = (ShadowStore<T>)shadowStore;
        store.Query<T>().ForEachEntity((ref T component, Entity entity) =>
        {
            if (shadows.ByEntityId.TryGetValue(entity.Id, out var shadow))
            {
                if (!diff.Equals(in component, in shadow))
                {
                    output.Add(new ReplicationDelta(entity, TypeId));
                    shadows.ByEntityId[entity.Id] = component;
                }
            }
            else
            {
                output.Add(new ReplicationDelta(entity, TypeId));   // 新实体 → 视为 dirty
                shadows.ByEntityId[entity.Id] = component;
            }
        });
    }
}
```

- [ ] **Step 4: 运行通过**

Run: `dotnet test tests/Gameplay.Tests/Gameplay.Tests.csproj -f net10.0 --filter "FullyQualifiedName~ReplicationRegistryTests"`
Expected: 全部通过。

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "添加 ReplicationRegistry 复制集注册与 shadow-diff 条目"
```

---

### Task 3: ReplicationPacket 编解码

**Files:**
- Create: `src/Gameplay/Gameplay.Replication/ReplicationPacket.cs`
- Test: `tests/Gameplay.Tests/Gameplay.Tests.Replication/ReplicationPacketTests.cs`

**Interfaces:**
- Consumes: `NetworkId`、`ReplicationRegistry`（`GetEntry`/`Entries`）、`ByteWriter`/`ByteReader`
- Produces:
  - `EReplicationPacketType`（enum：`Spawn=1`、`Update=2`、`Despawn=3`、`FullSnapshot=4`）
  - `ReplicationPacket`（static 编解码，见下方代码）

- [ ] **Step 1: 写失败测试**

`tests/Gameplay.Tests/Gameplay.Tests.Replication/ReplicationPacketTests.cs`：

```csharp
using System;
using Friflo.Engine.ECS;
using Gameplay.Core;
using Gameplay.Replication;
using Xunit;

namespace Gameplay.Tests.Replication;

public class ReplicationPacketTests
{
    [Fact]
    public void WriteReadSpawn_Roundtrips()
    {
        SerializerRegistry.Register(new SyncTestSerializer());
        ReplicationRegistry.Register<SyncTestComponent>(new SyncTestDiff());

        var store = new EntityStore();
        var entity = store.CreateEntity();
        entity.AddComponent(new SyncTestComponent { Value = 99 });

        Span<byte> buf = stackalloc byte[256];
        var writer = new ByteWriter(buf);
        ReplicationPacket.WriteSpawn(entity, new NetworkId(5), ref writer);

        var reader = new ByteReader(buf[..writer.BytesWritten]);
        var type = ReplicationPacket.ReadType(ref reader);
        Assert.Equal(EReplicationPacketType.Spawn, type);

        // 客户端解码：读 NetworkId → 建镜像 → 应用组件
        var netId = ReplicationPacket.ReadNetworkId(ref reader);
        Assert.Equal(5, netId.Value);
        var mirror = store.CreateEntity();
        ReplicationPacket.ReadComponents(mirror, ref reader);
        Assert.Equal(99, mirror.GetComponent<SyncTestComponent>().Value);
    }
}
```

- [ ] **Step 2: 运行确认失败**

Run: `dotnet test tests/Gameplay.Tests/Gameplay.Tests.csproj -f net10.0 --filter "FullyQualifiedName~ReplicationPacketTests"`
Expected: 编译错误（`ReplicationPacket` 未定义）。

- [ ] **Step 3: 实现 ReplicationPacket**

`src/Gameplay/Gameplay.Replication/ReplicationPacket.cs`：

```csharp
using System;
using Friflo.Engine.ECS;
using Gameplay.Core;

namespace Gameplay.Replication;

/// <summary>复制包类型。</summary>
public enum EReplicationPacketType : byte
{
    Spawn = 1,
    Update = 2,
    Despawn = 3,
    FullSnapshot = 4,
}

/// <summary>复制包编解码（单实体操作，type 判别）。</summary>
public static class ReplicationPacket
{
    /// <summary>写包类型头。</summary>
    public static void WriteType(EReplicationPacketType type, ref ByteWriter writer)
        => writer.Write((byte)type);

    public static EReplicationPacketType ReadType(ref ByteReader reader)
        => (EReplicationPacketType)reader.ReadByte();

    public static void WriteNetworkId(NetworkId id, ref ByteWriter writer)
        => writer.Write(id.Value);

    public static NetworkId ReadNetworkId(ref ByteReader reader)
        => new(reader.ReadInt());

    /// <summary>写 Spawn/Update 的组件负载：[count][typeId+data]*（typeId 由调用方决定是否全量/增量）。</summary>
    public static void WriteComponents(Entity entity, IReadOnlyList<int> typeIds, ref ByteWriter writer)
    {
        writer.Write(typeIds.Count);
        for (int i = 0; i < typeIds.Count; i++)
        {
            var entry = ReplicationRegistry.GetEntry(typeIds[i])!;
            writer.Write(typeIds[i]);
            entry.Capture(entity, ref writer);
        }
    }

    /// <summary>写 Spawn（组件全量）：NetworkId + [count][typeId+data]*。</summary>
    public static void WriteSpawn(Entity entity, NetworkId id, ref ByteWriter writer)
    {
        WriteType(EReplicationPacketType.Spawn, ref writer);
        WriteNetworkId(id, ref writer);
        var ids = GatherReplicatedTypeIds(entity);
        WriteComponents(entity, ids, ref writer);
    }

    /// <summary>读组件负载到实体（按 typeId 查 entry 应用，未知 typeId 抛异常 fail-fast）。</summary>
    public static void ReadComponents(Entity entity, ref ByteReader reader)
    {
        int count = reader.ReadInt();
        for (int i = 0; i < count; i++)
        {
            int typeId = reader.ReadInt();
            var entry = ReplicationRegistry.GetEntry(typeId)
                ?? throw new InvalidOperationException($"未知复制组件 typeId：{typeId}");
            entry.Apply(entity, ref reader);
        }
    }

    private static int[] GatherReplicatedTypeIds(Entity entity)
    {
        var list = new List<int>();
        foreach (var entry in ReplicationRegistry.Entries)
            if (entry.HasComponent(entity)) list.Add(entry.TypeId);
        return list.ToArray();
    }
}
```

- [ ] **Step 4: 运行通过**

Run: `dotnet test tests/Gameplay.Tests/Gameplay.Tests.csproj -f net10.0 --filter "FullyQualifiedName~ReplicationPacketTests"`
Expected: 全部通过。

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "添加 ReplicationPacket 编解码（Spawn/Update/Despawn/FullSnapshot）"
```

---

### Task 4: ReplicationServer（双集合 + NetworkId 分配 + 生命周期）

**Files:**
- Create: `src/Gameplay/Gameplay.Replication/ReplicationServer.cs`
- Test: `tests/Gameplay.Tests/Gameplay.Tests.Replication/ReplicationServerTests.cs`

**Interfaces:**
- Consumes: `NetworkId`、`ReplicationRegistry`、`IReplicationServerTransport`、`EntityLifecycle`（Core）、Friflo `EntityStore`、`OwnerComponent`（Core）
- Produces:
  - `ReplicationServer`（`ctor(EntityStore store, IReplicationServerTransport transport)`、`NetworkId GetNetworkId(int entityId)`、`void AddClient(int clientId)`、`void RemoveClient(int clientId)`、`void HandleLifecycle(in EntityLifecycleEvent evt)`、`void Tick()`）
  - `ClientState`（internal：`HashSet<NetworkId> Bubble`、`HashSet<NetworkId> Mirrored`、`bool NeedsSnapshot`）

- [ ] **Step 1: 写失败测试**

`tests/Gameplay.Tests/Gameplay.Tests.Replication/ReplicationServerTests.cs`：

```csharp
using Friflo.Engine.ECS;
using Gameplay.Core;
using Gameplay.Replication;
using Xunit;

namespace Gameplay.Tests.Replication;

public class ReplicationServerTests
{
    private sealed class NullServerTransport : IReplicationServerTransport
    {
        public void SendToClient(int clientId, ReadOnlySpan<byte> payload) { }
        public bool TryReceiveFromClient(int clientId, out ReadOnlySpan<byte> payload)
        {
            payload = default;
            return false;
        }
    }

    [Fact]
    public void ComponentAdded_AssignsNetworkId()
    {
        SerializerRegistry.Register(new SyncTestSerializer());
        ReplicationRegistry.Register<SyncTestComponent>(new SyncTestDiff());

        var world = new World(ENetMode.DedicatedServer);
        var server = new ReplicationServer(world.Store, new NullServerTransport());
        EntityLifecycle.Subscribe(world, server.HandleLifecycle);
        server.AddClient(0);

        var entity = world.Store.CreateEntity();
        entity.AddComponent(new SyncTestComponent { Value = 1 });

        Assert.True(server.GetNetworkId(entity.Id).IsValid);
    }
}
```

- [ ] **Step 2: 运行确认失败**

Run: `dotnet test tests/Gameplay.Tests/Gameplay.Tests.csproj -f net10.0 --filter "FullyQualifiedName~ReplicationServerTests"`
Expected: 编译错误（`ReplicationServer` 未定义）。

- [ ] **Step 3: 实现 ReplicationServer**

`src/Gameplay/Gameplay.Replication/ReplicationServer.cs`：

```csharp
using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Gameplay.Core;

namespace Gameplay.Replication;

/// <summary>客户端复制状态（每客户端一个）。</summary>
internal sealed class ClientState
{
    public readonly HashSet<NetworkId> Bubble = new();     // 应可见
    public readonly HashSet<NetworkId> Mirrored = new();   // 已发送（客户端已镜像）
    public bool NeedsSnapshot = true;                      // 新连接触发全量
}

/// <summary>服务端权威——Bubble/Mirrored 双集合 + NetworkId 分配 + spawn/despawn。</summary>
public sealed class ReplicationServer
{
    private readonly EntityStore store;
    private readonly IReplicationServerTransport transport;
    private readonly Dictionary<int, ClientState> clients = new();
    private readonly Dictionary<int, NetworkId> entityToNetId = new();
    private readonly Dictionary<NetworkId, int> netIdToEntity = new();
    private readonly Dictionary<int, IShadowStore> shadowStores = new();
    private int nextNetworkId = 1;

    public ReplicationServer(EntityStore store, IReplicationServerTransport transport)
    {
        this.store = store;
        this.transport = transport;
    }

    public NetworkId GetNetworkId(int entityId)
        => entityToNetId.TryGetValue(entityId, out var id) ? id : NetworkId.Invalid;

    public void AddClient(int clientId)
    {
        if (clients.ContainsKey(clientId)) return;
        clients[clientId] = new ClientState { NeedsSnapshot = true };
    }

    public void RemoveClient(int clientId) => clients.Remove(clientId);

    /// <summary>EntityLifecycle 回调（由 ReplicationModule 经 EntityLifecycle.Subscribe 接线）。</summary>
    public void HandleLifecycle(in EntityLifecycleEvent evt)
    {
        switch (evt.Type)
        {
            case EEntityLifecycleType.ComponentAdded:
                OnComponentAdded(evt.Entity, evt.ComponentType);
                break;
            case EEntityLifecycleType.EntityDeleted:
                OnEntityDeleted(evt.Entity);
                break;
        }
    }

    private void OnComponentAdded(Entity entity, ComponentType componentType)
    {
        if (entityToNetId.ContainsKey(entity.Id)) return;               // 已分配
        var entry = ReplicationRegistry.GetByComponentType(componentType);
        if (entry == null) return;                                      // 非复制组件

        var netId = new NetworkId(nextNetworkId++);
        entityToNetId[entity.Id] = netId;
        netIdToEntity[netId] = entity.Id;
        AddToBubbles(entity, netId);                                    // 只加 Bubble，不加 Mirrored
    }

    private void AddToBubbles(Entity entity, NetworkId netId)
    {
        int owner = entity.HasComponent<OwnerComponent>()
            ? entity.GetComponent<OwnerComponent>().PlayerId
            : -1;
        foreach (var (clientId, state) in clients)
        {
            if (owner == -1 || owner == clientId)
                state.Bubble.Add(netId);
        }
    }

    private void OnEntityDeleted(Entity entity)
    {
        if (!entityToNetId.TryGetValue(entity.Id, out var netId)) return;
        entityToNetId.Remove(entity.Id);
        netIdToEntity.Remove(netId);
        foreach (var state in clients.Values)
        {
            state.Bubble.Remove(netId);
            state.Mirrored.Remove(netId);
        }
        // 广播 despawn
        Span<byte> buf = stackalloc byte[16];
        var writer = new ByteWriter(buf);
        ReplicationPacket.WriteType(EReplicationPacketType.Despawn, ref writer);
        ReplicationPacket.WriteNetworkId(netId, ref writer);
        foreach (var clientId in clients.Keys)
            transport.SendToClient(clientId, buf[..writer.BytesWritten]);
    }

    /// <summary>每帧由 ReplicationSystem 驱动（shadow-diff → spawn/update → 发送）。</summary>
    public void Tick()
    {
        // 懒建 shadow store
        foreach (var entry in ReplicationRegistry.Entries)
            if (!shadowStores.ContainsKey(entry.TypeId))
                shadowStores[entry.TypeId] = entry.CreateShadowStore();

        var deltas = new List<ReplicationDelta>();
        foreach (var entry in ReplicationRegistry.Entries)
            entry.Diff(shadowStores[entry.TypeId], store, deltas);

        foreach (var (clientId, state) in clients)
        {
            if (state.NeedsSnapshot) { SendSnapshot(clientId, state); continue; }
            foreach (var d in deltas)
            {
                var netId = GetNetworkId(d.Entity.Id);
                if (!netId.IsValid) continue;
                if (state.Bubble.Contains(netId) && !state.Mirrored.Contains(netId))
                    SendSpawn(clientId, state, d.Entity, netId);
                else if (state.Bubble.Contains(netId))
                    SendUpdate(clientId, d.Entity, netId, d.TypeId);
            }
        }
    }

    private void SendSpawn(int clientId, ClientState state, Entity entity, NetworkId netId)
    {
        Span<byte> buf = stackalloc byte[512];
        var writer = new ByteWriter(buf);
        ReplicationPacket.WriteSpawn(entity, netId, ref writer);
        transport.SendToClient(clientId, buf[..writer.BytesWritten]);
        state.Mirrored.Add(netId);
    }

    private void SendUpdate(int clientId, Entity entity, NetworkId netId, int typeId)
    {
        Span<byte> buf = stackalloc byte[128];
        var writer = new ByteWriter(buf);
        ReplicationPacket.WriteType(EReplicationPacketType.Update, ref writer);
        ReplicationPacket.WriteNetworkId(netId, ref writer);
        var entry = ReplicationRegistry.GetEntry(typeId)!;
        writer.Write(1);               // count
        writer.Write(typeId);
        entry.Capture(entity, ref writer);
        transport.SendToClient(clientId, buf[..writer.BytesWritten]);
    }

    private void SendSnapshot(int clientId, ClientState state)
    {
        // v1 全量快照：Bubble 内全部实体组件全量（简化实现，实体少时直接逐实体发 Spawn）
        foreach (var netId in state.Bubble)
        {
            var entity = store.GetEntityById(netIdToEntity[netId]);
            if (entity.IsNull) continue;
            SendSpawn(clientId, state, entity, netId);
        }
        state.NeedsSnapshot = false;
    }
}
```

- [ ] **Step 4: 运行通过**

Run: `dotnet test tests/Gameplay.Tests/Gameplay.Tests.csproj -f net10.0 --filter "FullyQualifiedName~ReplicationServerTests"`
Expected: 全部通过。

> **注意**：`foreach (var (clientId, state) in clients)` 依赖 `KeyValuePair<TKey,TValue>.Deconstruct`（.NET Core 2.0+ 已提供），可直接用。`stackalloc` 缓冲区大小按实际组件量调整（v1 测试组件小，512B 足够；组件量大改 ArrayPool）。`SendSnapshot` 在 v1 简化为「逐实体发 Spawn」（功能等价全量快照），`EFullSnapshot` 包类型已定义但 v1 服务端不产出，留待后续。

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "添加 ReplicationServer 服务端权威（双集合 + NetworkId + 生命周期）"
```

---

### Task 5: ReplicationClient（镜像 + 映射）

**Files:**
- Create: `src/Gameplay/Gameplay.Replication/ReplicationClient.cs`
- Test: `tests/Gameplay.Tests/Gameplay.Tests.Replication/ReplicationClientTests.cs`

**Interfaces:**
- Consumes: `NetworkId`、`ReplicationRegistry`、`IReplicationClientTransport`、Friflo `EntityStore`
- Produces:
  - `ReplicationClient`（`ctor(EntityStore store, IReplicationClientTransport transport)`、`void ApplyServerPacket(ReadOnlySpan<byte> payload)`、`Entity? GetMirror(NetworkId id)`）

- [ ] **Step 1: 写失败测试**

`tests/Gameplay.Tests/Gameplay.Tests.Replication/ReplicationClientTests.cs`：

```csharp
using System;
using Friflo.Engine.ECS;
using Gameplay.Core;
using Gameplay.Replication;
using Xunit;

namespace Gameplay.Tests.Replication;

public class ReplicationClientTests
{
    private sealed class NullClientTransport : IReplicationClientTransport
    {
        public bool TryReceiveFromServer(out ReadOnlySpan<byte> payload)
        {
            payload = default;
            return false;
        }
        public void SendToServer(ReadOnlySpan<byte> payload) { }
    }

    [Fact]
    public void ApplySpawn_CreatesMirror()
    {
        SerializerRegistry.Register(new SyncTestSerializer());
        ReplicationRegistry.Register<SyncTestComponent>(new SyncTestDiff());

        var client = new ReplicationClient(new EntityStore(), new NullClientTransport());

        // 用服务端实体产出 Spawn 包
        var sourceStore = new EntityStore();
        var sourceEntity = sourceStore.CreateEntity();
        sourceEntity.AddComponent(new SyncTestComponent { Value = 55 });

        Span<byte> buf = stackalloc byte[128];
        var writer = new ByteWriter(buf);
        ReplicationPacket.WriteSpawn(sourceEntity, new NetworkId(3), ref writer);

        client.ApplyServerPacket(buf[..writer.BytesWritten]);

        var mirror = client.GetMirror(new NetworkId(3));
        Assert.False(mirror.IsNull);
        Assert.Equal(55, mirror.GetComponent<SyncTestComponent>().Value);
    }
}
```

> 测试用 `ReplicationPacket.WriteSpawn`（Task 3 已有）产出 Spawn 包，避免测试依赖内部实现。

- [ ] **Step 2: 运行确认失败**

Run: `dotnet test tests/Gameplay.Tests/Gameplay.Tests.csproj -f net10.0 --filter "FullyQualifiedName~ReplicationClientTests"`
Expected: 编译错误（`ReplicationClient` 未定义）。

- [ ] **Step 3: 实现 ReplicationClient**

`src/Gameplay/Gameplay.Replication/ReplicationClient.cs`：

```csharp
using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Gameplay.Core;

namespace Gameplay.Replication;

/// <summary>客户端镜像——NetworkId → 本地镜像实体映射，接收并应用服务端包。</summary>
public sealed class ReplicationClient
{
    private readonly EntityStore store;
    private readonly IReplicationClientTransport transport;
    private readonly Dictionary<NetworkId, Entity> mirror = new();

    public ReplicationClient(EntityStore store, IReplicationClientTransport transport)
    {
        this.store = store;
        this.transport = transport;
    }

    /// <summary>按 NetworkId 查镜像实体（无则 IsNull）。</summary>
    public Entity GetMirror(NetworkId id)
        => mirror.TryGetValue(id, out var e) ? e : default;

    /// <summary>处理一条服务端下发的包（由 ReplicationClientSystem 每帧调用）。</summary>
    public void ApplyServerPacket(ReadOnlySpan<byte> payload)
    {
        var reader = new ByteReader(payload);
        var type = ReplicationPacket.ReadType(ref reader);
        switch (type)
        {
            case EReplicationPacketType.Spawn:
                var spawnId = ReplicationPacket.ReadNetworkId(ref reader);
                var spawnEntity = store.CreateEntity();
                ReplicationPacket.ReadComponents(spawnEntity, ref reader);
                mirror[spawnId] = spawnEntity;
                break;

            case EReplicationPacketType.Update:
                var updateId = ReplicationPacket.ReadNetworkId(ref reader);
                if (mirror.TryGetValue(updateId, out var updateEntity))
                    ReplicationPacket.ReadComponents(updateEntity, ref reader);
                break;

            case EReplicationPacketType.Despawn:
                var despawnId = ReplicationPacket.ReadNetworkId(ref reader);
                if (mirror.TryGetValue(despawnId, out var despawnEntity))
                {
                    mirror.Remove(despawnId);
                    if (!despawnEntity.IsNull) despawnEntity.DeleteEntity();
                }
                break;

            case EReplicationPacketType.FullSnapshot:
                ApplySnapshot(ref reader);
                break;

            default:
                throw new InvalidOperationException($"未知复制包类型：{(byte)type}");
        }
    }

    private void ApplySnapshot(ref ByteReader reader)
    {
        int count = reader.ReadInt();
        for (int i = 0; i < count; i++)
        {
            var id = ReplicationPacket.ReadNetworkId(ref reader);
            Entity entity;
            if (!mirror.TryGetValue(id, out entity))
            {
                entity = store.CreateEntity();
                mirror[id] = entity;
            }
            ReplicationPacket.ReadComponents(entity, ref reader);
        }
    }
}
```

- [ ] **Step 4: 运行通过**

Run: `dotnet test tests/Gameplay.Tests/Gameplay.Tests.csproj -f net10.0 --filter "FullyQualifiedName~ReplicationClientTests"`
Expected: 全部通过。

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "添加 ReplicationClient 客户端镜像与映射"
```

---

### Task 6: ReplicationSystem + ReplicationClientSystem

**Files:**
- Create: `src/Gameplay/Gameplay.Replication/ReplicationSystem.cs`
- Create: `src/Gameplay/Gameplay.Replication/ReplicationClientSystem.cs`

**Interfaces:**
- Consumes: `ReplicationServer`（`Tick()`）、`ReplicationClient`（`ApplyServerPacket`）、`IReplicationClientTransport`（`TryReceiveFromServer`）、Friflo `BaseSystem`
- Produces:
  - `ReplicationSystem : BaseSystem`（`ctor(ReplicationServer server)`，`OnUpdateGroup` 调 `server.Tick()`）
  - `ReplicationClientSystem : BaseSystem`（`ctor(ReplicationClient client, IReplicationClientTransport transport)`，`OnUpdateGroup` 轮询 `TryReceiveFromServer` → `client.ApplyServerPacket`）

- [ ] **Step 1: 实现两个 System**（薄封装，靠 Task 8 端到端测试覆盖）

`src/Gameplay/Gameplay.Replication/ReplicationSystem.cs`：

```csharp
using Friflo.Engine.ECS.Systems;

namespace Gameplay.Replication;

/// <summary>服务端每帧复制 System（挂 PostSimulation，Simulation 改完组件后跑 shadow-diff 发送）。</summary>
public sealed class ReplicationSystem : BaseSystem
{
    private readonly ReplicationServer server;

    public ReplicationSystem(ReplicationServer server) => this.server = server;

    protected override void OnUpdateGroup() => server.Tick();
}
```

`src/Gameplay/Gameplay.Replication/ReplicationClientSystem.cs`：

```csharp
using System;
using Friflo.Engine.ECS.Systems;

namespace Gameplay.Replication;

/// <summary>客户端每帧镜像 System（挂 PreSimulation，模拟前应用服务端状态）。</summary>
public sealed class ReplicationClientSystem : BaseSystem
{
    private readonly ReplicationClient client;
    private readonly IReplicationClientTransport transport;

    public ReplicationClientSystem(ReplicationClient client, IReplicationClientTransport transport)
    {
        this.client = client;
        this.transport = transport;
    }

    protected override void OnUpdateGroup()
    {
        while (transport.TryReceiveFromServer(out var payload))
            client.ApplyServerPacket(payload);
    }
}
```

- [ ] **Step 2: 构建通过**

Run: `dotnet build src/Gameplay/Gameplay.csproj`
Expected: 构建成功（这两个 System 的端到端行为由 Task 8 覆盖，本任务只保证编译）。

- [ ] **Step 3: Commit**

```bash
git add -A
git commit -m "添加 ReplicationSystem / ReplicationClientSystem 每帧驱动"
```

---

### Task 7: ReplicationModule

**Files:**
- Create: `src/Gameplay/Gameplay.Replication/ReplicationModule.cs`

**Interfaces:**
- Consumes: `World`（`NetMode`/`Store`/`AddSystem`/`RegisterService`）、`ReplicationServer`/`ReplicationClient`/`ReplicationSystem`/`ReplicationClientSystem`、`EntityLifecycle`、`ENetMode`
- Produces: `ReplicationModule : IModule`（`ctor(World world, IReplicationServerTransport? serverTransport, IReplicationClientTransport? clientTransport)`，`ReplicationServer? Server`、`ReplicationClient? Client`）

- [ ] **Step 1: 实现 ReplicationModule**

`src/Gameplay/Gameplay.Replication/ReplicationModule.cs`：

```csharp
using Gameplay.Core;

namespace Gameplay.Replication;

/// <summary>状态复制模块——按 NetMode 挂载服务端/客户端复制。</summary>
public sealed class ReplicationModule : IModule
{
    public ReplicationServer? Server { get; }
    public ReplicationClient? Client { get; }

    public ReplicationModule(World world, IReplicationServerTransport? serverTransport, IReplicationClientTransport? clientTransport)
    {
        var netMode = world.NetMode;

#if GP_WITH_SERVER_CODE
        if ((netMode == ENetMode.DedicatedServer || netMode == ENetMode.ListenServer) && serverTransport != null)
        {
            Server = new ReplicationServer(world.Store, serverTransport);
            world.RegisterService(Server);
            world.AddSystem(new ReplicationSystem(Server), ESimulationStage.PostSimulation);
            EntityLifecycle.Subscribe(world, Server.HandleLifecycle);
        }
#endif

#if !GP_SERVER
        if (netMode == ENetMode.Client && clientTransport != null)
        {
            Client = new ReplicationClient(world.Store, clientTransport);
            world.RegisterService(Client);
            world.AddSystem(new ReplicationClientSystem(Client, clientTransport), ESimulationStage.PreSimulation);
        }
#endif
    }
}
```

> **注意**：`ListenServer`（Host）只挂服务端（本地玩家直接读权威状态，不建镜像 World），故客户端分支条件仅 `ENetMode.Client`。`Standalone` 两者都不挂。

- [ ] **Step 2: 构建通过（三模式）**

Run: `dotnet build src/Gameplay/Gameplay.csproj -p:GameplayMode=Client`
Run: `dotnet build src/Gameplay/Gameplay.csproj -p:GameplayMode=Host`
Run: `dotnet build src/Gameplay/Gameplay.csproj -p:GameplayMode=Server`
Expected: 三模式均构建成功。

- [ ] **Step 3: Commit**

```bash
git add -A
git commit -m "添加 ReplicationModule 按 NetMode 挂载复制"
```

---

### Task 8: Loopback 传输 + 端到端同步

**Files:**
- Create: `tests/Gameplay.Tests/Gameplay.Tests.Replication/LoopbackReplicationTransport.cs`
- Test: `tests/Gameplay.Tests/Gameplay.Tests.Replication/ReplicationSyncTests.cs`

**Interfaces:**
- Consumes: `ReplicationModule`、`ReplicationServer`、`ReplicationClient`、传输接口、`World`、`SyncTestComponent`
- Produces: `LoopbackServerTransport` / `LoopbackClientTransport`（测试内存传输）

- [ ] **Step 1: 实现 loopback 传输**

`tests/Gameplay.Tests/Gameplay.Tests.Replication/LoopbackReplicationTransport.cs`：

```csharp
using System;
using System.Collections.Generic;
using Gameplay.Replication;

namespace Gameplay.Tests.Replication;

/// <summary>内存回环服务端传输——把消息路由到各客户端的入队。</summary>
public sealed class LoopbackServerTransport : IReplicationServerTransport
{
    private readonly Dictionary<int, Queue<byte[]>> clientQueues = new();
    private readonly List<int> clientIds = new();

    public void RegisterClient(int clientId, Queue<byte[]> queue)
    {
        clientQueues[clientId] = queue;
        clientIds.Add(clientId);
    }

    public IReadOnlyList<int> ClientIds => clientIds;

    public void SendToClient(int clientId, ReadOnlySpan<byte> payload)
        => clientQueues[clientId].Enqueue(payload.ToArray());

    public bool TryReceiveFromClient(int clientId, out ReadOnlySpan<byte> payload)
    {
        payload = default;
        return false;   // v1 无上行
    }
}

/// <summary>内存回环客户端传输——从本客户端的入队拉取。</summary>
public sealed class LoopbackClientTransport : IReplicationClientTransport
{
    private readonly Queue<byte[]> incoming = new();

    public Queue<byte[]> Queue => incoming;

    public bool TryReceiveFromServer(out ReadOnlySpan<byte> payload)
    {
        if (incoming.Count == 0)
        {
            payload = default;
            return false;
        }
        var data = incoming.Dequeue();
        payload = data;
        return true;
    }

    public void SendToServer(ReadOnlySpan<byte> payload) { }   // v1 无上行
}
```

- [ ] **Step 2: 写端到端失败测试**

`tests/Gameplay.Tests/Gameplay.Tests.Replication/ReplicationSyncTests.cs`：

```csharp
using Friflo.Engine.ECS;
using Gameplay.Core;
using Gameplay.Replication;
using Xunit;

namespace Gameplay.Tests.Replication;

public class ReplicationSyncTests
{
    [Fact]
    public void ServerChange_MirrorsToClient()
    {
        SerializerRegistry.Register(new SyncTestSerializer());
        ReplicationRegistry.Register<SyncTestComponent>(new SyncTestDiff());

        // 服务端权威 World
        var serverWorld = new World(ENetMode.DedicatedServer);
        var serverTransport = new LoopbackServerTransport();

        // 客户端镜像 World
        var clientWorld = new World(ENetMode.Client);
        var clientTransport = new LoopbackClientTransport();
        serverTransport.RegisterClient(0, clientTransport.Queue);

        var serverModule = new ReplicationModule(serverWorld, serverTransport, null);
        var clientModule = new ReplicationModule(clientWorld, null, clientTransport);
        serverModule.Server!.AddClient(0);

        // 服务端创建实体
        var serverEntity = serverWorld.Store.CreateEntity();
        serverEntity.AddComponent(new SyncTestComponent { Value = 10 });

        // 跑一帧：服务端发送 → 客户端接收应用
        serverWorld.Update(0.16f);
        clientWorld.Update(0.16f);

        var mirror = clientModule.Client!.GetMirror(serverModule.Server!.GetNetworkId(serverEntity.Id));
        Assert.False(mirror.IsNull);
        Assert.Equal(10, mirror.GetComponent<SyncTestComponent>().Value);
    }
}
```

- [ ] **Step 3: 运行确认失败**

Run: `dotnet test tests/Gameplay.Tests/Gameplay.Tests.csproj -f net10.0 --filter "FullyQualifiedName~ReplicationSyncTests"`
Expected: 编译错误（`LoopbackServerTransport`/`LoopbackClientTransport` 未定义）。

- [ ] **Step 4: 运行通过**

Run: `dotnet test tests/Gameplay.Tests/Gameplay.Tests.csproj -f net10.0 --filter "FullyQualifiedName~ReplicationSyncTests"`
Expected: 全部通过（证明「服务端改组件 → 客户端镜像同步」闭环）。

- [ ] **Step 5: 补充 dirty/可见性测试**

在同文件追加两个测试：

```csharp
    [Fact]
    public void Dirty_OnlySendsChangedComponent()
    {
        // （省略注册样板）服务端改组件值后，客户端第二次 Update 收到 EUpdate 而非重复 Spawn
        // 断言：mirror 值更新到新值，且 GetMirror 仍指向同一镜像实体（未被重建）
    }

    [Fact]
    public void OwnerBased_Visibility_FiltersClients()
    {
        // 两个客户端 + 一个 OwnerComponent.PlayerId==0 的实体 → 客户端 0 收到、客户端 1 不收到
    }
```

> 这两个测试的完整断言由执行者按「dirty 只发变化组件」「Owner-based 相关性」语义补齐——`dirty` 测试断言 `mirror` 身份不变（`GetMirror` 返回同一 entity）且值更新；`visibility` 测试断言 `clientModule1.Client.GetMirror` 为 null 而 `clientModule0` 非 null。

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "添加 loopback 传输与端到端复制同步测试"
```

---

### Task 9: [Replicated] 特性 + ReplicationGenerator SG + 标记 Core 组件

**Files:**
- Create: `src/Gameplay.Shared/ReplicatedAttribute.cs`
- Create: `src/Gameplay.CodeGen/ReplicationGenerator.cs`
- Modify: 标记 4 个 Core 组件（加 `[Replicated]`）
  - `src/Gameplay/Gameplay.Core/Components/TransformComponent.cs`
  - `src/Gameplay/Gameplay.Core/Components/VelocityComponent.cs`
  - `src/Gameplay/Gameplay.Core/Components/HealthComponent.cs`
  - `src/Gameplay/Gameplay.Core/Components/OwnerComponent.cs`
- Test: `tests/Gameplay.Tests/Gameplay.Tests.Replication/ReplicationGeneratorTests.cs`

**Interfaces:**
- Consumes: `IComponentSerializer<T>`/`SerializerRegistry`（Core）、`IReplicationDiff<T>`/`ReplicationRegistry`（Task 1-2）、`ByteWriter`/`ByteReader`、Roslyn API
- Produces: `ReplicatedAttribute`（`namespace Gameplay`）、`ReplicationGenerator : IIncrementalGenerator`（生成 `XxxSerializer`/`XxxReplication`/`ReplicatedComponentRegistration.RegisterAll`）

- [ ] **Step 1: 创建 [Replicated] 特性**

`src/Gameplay.Shared/ReplicatedAttribute.cs`：

```csharp
using System;

namespace Gameplay;

/// <summary>标记 struct 组件参与网络复制。SG 扫描生成 serializer + diff + RegisterAll。</summary>
[AttributeUsage(AttributeTargets.Struct)]
public class ReplicatedAttribute : System.Attribute { }
```

- [ ] **Step 2: 实现 ReplicationGenerator**

`src/Gameplay.CodeGen/ReplicationGenerator.cs`：

```csharp
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;

namespace Gameplay.CodeGen;

/// <summary>扫描 [Replicated] 组件，生成 serializer + diff + RegisterAll 三件套。</summary>
[Generator]
public class ReplicationGenerator : IIncrementalGenerator
{
    private const string ReplicatedShortName = "Replicated";
    private const string ReplicatedFullName = "ReplicatedAttribute";
    private const string RegistryFullName = "Gameplay.Replication.ReplicationRegistry";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var compilation = context.CompilationProvider;
        var components = context.SyntaxProvider.CreateSyntaxProvider(
            predicate: static (node, _) => node is TypeDeclarationSyntax tds && HasReplicated(tds),
            transform: static (ctx, _) => TransformComponent(ctx)
        ).Where(static info => info.StructName != null);

        var combined = components.Collect().Combine(compilation);
        context.RegisterSourceOutput(combined, static (spc, pair) => GenerateCode(spc, pair.Left, pair.Right));
    }

    private static bool HasReplicated(TypeDeclarationSyntax typeDecl)
    {
        foreach (var list in typeDecl.AttributeLists)
            foreach (var attr in list.Attributes)
            {
                var name = attr.Name.ToString();
                if (name == ReplicatedShortName || name == ReplicatedFullName)
                    return true;
            }
        return false;
    }

    private static ComponentInfo TransformComponent(GeneratorSyntaxContext ctx)
    {
        var typeDecl = (TypeDeclarationSyntax)ctx.Node;
        var typeSymbol = ctx.SemanticModel.GetDeclaredSymbol(typeDecl) as INamedTypeSymbol;
        if (typeSymbol == null || typeSymbol.TypeKind != TypeKind.Structure)
            return default;

        var fields = new List<FieldInfo>();
        foreach (var member in typeSymbol.GetMembers())
        {
            if (member is not IFieldSymbol field || field.IsStatic || field.IsImplicitlyDeclared)
                continue;
            var kind = Classify(field.Type);
            if (kind == FieldKind.Unsupported)
                return default;   // 不支持的字段类型 → 跳过该组件（诊断由编译期缺失序列化器暴露）
            fields.Add(new FieldInfo { Name = field.Name, Kind = kind, TypeName = field.Type.Name });
        }

        return new ComponentInfo
        {
            StructNamespace = typeSymbol.ContainingNamespace?.ToDisplayString() ?? string.Empty,
            StructName = typeSymbol.Name,
            Fields = fields,
        };
    }

    private static FieldKind Classify(ITypeSymbol type)
    {
        switch (type.SpecialType)
        {
            case SpecialType.System_Int32:
            case SpecialType.System_Single:
            case SpecialType.System_Boolean:
                return FieldKind.Primitive;
        }
        if (type.TypeKind == TypeKind.Enum)
            return FieldKind.Enum;
        if (type.Name == "Vector3" && type.ContainingNamespace?.ToDisplayString() == "Gameplay.Core")
            return FieldKind.Vector3;
        if (type.Name == "Quaternion" && type.ContainingNamespace?.ToDisplayString() == "Gameplay.Core")
            return FieldKind.Quaternion;
        return FieldKind.Unsupported;
    }

    private static void GenerateCode(SourceProductionContext spc, ImmutableArray<ComponentInfo> components, Compilation compilation)
    {
        if (components.IsDefaultOrEmpty) return;
        var sorted = components.Sort(static (a, b) => string.CompareOrdinal(a.StructName, b.StructName));

        // 每个组件生成 XxxSerializer + XxxReplication（任何程序集，只要组件标了 [Replicated]）
        foreach (var c in sorted)
        {
            spc.AddSource($"{c.StructNamespace}.{c.StructName}.Replication.g.cs", GeneratePerComponent(c));
        }

        // RegisterAll 只在定义 ReplicationRegistry 的程序集生成
        if (HasReplicationRegistry(compilation))
            spc.AddSource("ReplicatedComponentRegistration.g.cs", GenerateRegisterAll(sorted));
    }

    private static bool HasReplicationRegistry(Compilation compilation)
        => compilation.GetTypeByMetadataName(RegistryFullName) != null
           && SymbolEqualityComparer.Default.Equals(
               compilation.GetTypeByMetadataName(RegistryFullName)!.ContainingAssembly,
               compilation.Assembly);

    private static string GeneratePerComponent(ComponentInfo c)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("using Friflo.Engine.ECS;");
        sb.AppendLine("using Gameplay.Core;");
        sb.AppendLine("using Gameplay.Replication;");
        sb.AppendLine();
        if (!string.IsNullOrEmpty(c.StructNamespace))
            sb.AppendLine($"namespace {c.StructNamespace};");
        sb.AppendLine();

        // Serializer
        sb.AppendLine($"public sealed class {c.StructName}Serializer : IComponentSerializer<{c.StructName}>");
        sb.AppendLine("{");
        sb.AppendLine($"    public void Write(in {c.StructName} c, ref ByteWriter w)");
        sb.AppendLine("    {");
        foreach (var f in c.Fields)
            sb.AppendLine($"        {WriteExpr(f, "c")}");
        sb.AppendLine("    }");
        sb.AppendLine($"    public void Read(ref {c.StructName} c, ref ByteReader r)");
        sb.AppendLine("    {");
        foreach (var f in c.Fields)
            sb.AppendLine($"        {ReadExpr(f, "c")}");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        sb.AppendLine();

        // Diff
        sb.AppendLine($"public readonly struct {c.StructName}Replication : IReplicationDiff<{c.StructName}>");
        sb.AppendLine("{");
        sb.AppendLine($"    public bool Equals(in {c.StructName} a, in {c.StructName} b)");
        sb.AppendLine($"        => {EqualsExpr(c)};");
        sb.AppendLine("}");
        return sb.ToString();
    }

    private static string GenerateRegisterAll(ImmutableArray<ComponentInfo> sorted)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("using Gameplay.Core;");
        sb.AppendLine("using Gameplay.Replication;");
        sb.AppendLine();
        sb.AppendLine("namespace Gameplay.Replication;");
        sb.AppendLine();
        sb.AppendLine("public static class ReplicatedComponentRegistration");
        sb.AppendLine("{");
        sb.AppendLine("    public static void RegisterAll()");
        sb.AppendLine("    {");
        foreach (var c in sorted)
        {
            sb.AppendLine($"        SerializerRegistry.Register(new {c.StructName}Serializer());");
            sb.AppendLine($"        ReplicationRegistry.Register<{c.StructName}>(new {c.StructName}Replication());");
        }
        sb.AppendLine("    }");
        sb.AppendLine("}");
        return sb.ToString();
    }

    private static string WriteExpr(FieldInfo f, string obj)
        => f.Kind switch
        {
            FieldKind.Primitive => $"w.Write({obj}.{f.Name});",
            FieldKind.Enum => $"w.Write((int){obj}.{f.Name});",
            FieldKind.Vector3 => $"w.Write(in {obj}.{f.Name});",
            FieldKind.Quaternion => $"w.Write(in {obj}.{f.Name});",
            _ => string.Empty,
        };

    private static string ReadExpr(FieldInfo f, string obj)
        => f.Kind switch
        {
            FieldKind.Primitive => $"{obj}.{f.Name} = r.Read{PrimitiveRead(f)}();",
            FieldKind.Enum => $"{obj}.{f.Name} = ({f.TypeName})r.ReadInt();",
            FieldKind.Vector3 => $"{obj}.{f.Name} = r.ReadVector3();",
            FieldKind.Quaternion => $"{obj}.{f.Name} = r.ReadQuaternion();",
            _ => string.Empty,
        };

    private static string PrimitiveRead(FieldInfo f)
        => f.TypeName switch
        {
            "Int32" => "Int",
            "Single" => "Float",
            "Boolean" => "Bool",
            _ => "Int",
        };

    private static string EqualsExpr(ComponentInfo c)
    {
        if (c.Fields.Count == 0) return "true";
        var parts = new List<string>();
        foreach (var f in c.Fields)
            parts.Add($"a.{f.Name} == b.{f.Name}");
        return string.Join(" && ", parts);
    }

    private enum FieldKind { Primitive, Enum, Vector3, Quaternion, Unsupported }

    private struct FieldInfo { public string Name; public FieldKind Kind; public string TypeName; }

    private struct ComponentInfo
    {
        public string StructNamespace;
        public string StructName;
        public List<FieldInfo> Fields;
    }
}
```

- [ ] **Step 3: 标记 4 个 Core 组件**

对 `TransformComponent` / `VelocityComponent` / `HealthComponent` / `OwnerComponent` 加 `[Replicated]`（`using Gameplay;` + 特性）。例 `HealthComponent.cs`：

```csharp
using Friflo.Engine.ECS;
using Gameplay;

namespace Gameplay.Core;

/// <summary>通用生命值 + 存活标记（死亡中间态）。</summary>
[Replicated]
public struct HealthComponent : IComponent
{
    public float Current;
    public float Max;
    public bool IsAlive;
}
```

> `TransformComponent` 含 `Vector3`/`Quaternion`/`float Scale` 字段，`VelocityComponent` 含 `Vector3`，`OwnerComponent` 含 `int PlayerId`——均在 SG 支持的字段类型内。

- [ ] **Step 4: 写生成代码失败测试**

`tests/Gameplay.Tests/Gameplay.Tests.Replication/ReplicationGeneratorTests.cs`：

```csharp
using System;
using Friflo.Engine.ECS;
using Gameplay.Core;
using Gameplay.Replication;
using Xunit;

namespace Gameplay.Tests.Replication;

public class ReplicationGeneratorTests
{
    [Fact]
    public void RegisterAll_RegistersCoreComponents()
    {
        ReplicatedComponentRegistration.RegisterAll();
        // 注册后 HealthComponent 的 serializer + diff 可用
        Assert.NotNull(SerializerRegistry.Get<HealthComponent>());
        Assert.NotNull(ReplicationRegistry.GetByComponentType(ComponentType<HealthComponent>.Value));
    }

    [Fact]
    public void GeneratedSerializer_Roundtrips()
    {
        ReplicatedComponentRegistration.RegisterAll();
        var store = new EntityStore();
        var entity = store.CreateEntity();
        entity.AddComponent(new HealthComponent { Current = 75f, Max = 100f, IsAlive = true });

        var entry = ReplicationRegistry.GetByComponentType(ComponentType<HealthComponent>.Value)!;
        Span<byte> buf = stackalloc byte[64];
        var writer = new ByteWriter(buf);
        entry.Capture(entity, ref writer);
        ref var health = ref entity.GetComponent<HealthComponent>();
        health.Current = 1f;

        var reader = new ByteReader(buf[..writer.BytesWritten]);
        entry.Apply(entity, ref reader);

        Assert.Equal(75f, entity.GetComponent<HealthComponent>().Current);
    }
}
```

> **注意**：`ReplicationRegistry` 是 `static class`（Task 2），故 `RegisterAll()` 无参，直接调用静态 `SerializerRegistry.Register(...)` 与 `ReplicationRegistry.Register<T>(...)`。全局静态注册在测试间残留（重复注册幂等替换、不追加），与现有 `SerializationTests` 同款，接受。

- [ ] **Step 5: 运行确认失败**

Run: `dotnet test tests/Gameplay.Tests/Gameplay.Tests.csproj -f net10.0 --filter "FullyQualifiedName~ReplicationGeneratorTests"`
Expected: 编译错误（`ReplicatedComponentRegistration` 未生成 / `[Replicated]` 未定义）。

- [ ] **Step 6: 运行通过 + 全量回归**

Run: `dotnet test tests/Gameplay.Tests/Gameplay.Tests.csproj -f net10.0`
Expected: 全部通过（含既有 Core/Abilities/Tasks/Tags 测试 + 新增 Replication 测试）。

- [ ] **Step 7: Commit**

```bash
git add -A
git commit -m "添加 [Replicated] 特性与 ReplicationGenerator 源生成器"
```

---

## Self-Review 备注

- **Spec 覆盖**：§4.1（SG 三件套）→ Task 9；§4.2（ReplicationRegistry）→ Task 2；§4.3（NetworkId）→ Task 1；§4.4（shadow-diff）→ Task 2/4；§4.5（包格式）→ Task 3；§4.6（传输接口）→ Task 1/8；§5（服务端）→ Task 4；§6（客户端）→ Task 5；§7（Module/Host）→ Task 7；§9（测试）→ Task 8/9。
- **已知执行时需核实的 Friflo/Roslyn API**：`ComponentType<T>.Value` 形态、`store.Query<T>().ForEachEntity` 回调签名、`ByteReader.ReadByte`（若缺则从 `ReadInt` 后取低字节）、`RegisterAll` 的 static 调用形态（见 Task 9 注意）。执行者以 Friflo 源码（`../Friflo.Engine.ECS/src/ECS/`）与现有 `GameplayAttributeGenerator`/`GameplayEventGenerator` 为准微调，不改变设计。
- **类型一致性**：`NetworkId.Value`（int）贯穿 Task 1-5；`IReplicationEntry.Capture/Apply` 不写 typeId（typeId 由 `ReplicationPacket` 写）贯穿 Task 2-3；`ReplicationServer` 的 `clients` 存 `ClientState{Bubble,Mirrored,NeedsSnapshot}` 贯穿 Task 4-6；`ReplicationClient.ApplyServerPacket` 签名贯穿 Task 5-6-8。
- **Task 9 的 `RegisterAll` 签名**：spec §4.1 写 `RegisterAll(ReplicationRegistry)`，实现为 static 更简单——执行时统一为 `public static void RegisterAll()`（无参）并相应改测试，保证与 `SerializerRegistry`（static）风格一致。
