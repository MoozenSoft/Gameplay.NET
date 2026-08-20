using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
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

    /// <summary>记录 (clientId, 包类型) 的测试传输，用于断言谁收到了什么。</summary>
    private sealed class RecordingTransport : IReplicationServerTransport
    {
        public readonly List<(int ClientId, EReplicationPacketType Type)> Sent = new();

        public void SendToClient(int clientId, ReadOnlySpan<byte> payload)
            => Sent.Add((clientId, (EReplicationPacketType)payload[0]));

        public bool TryReceiveFromClient(int clientId, out ReadOnlySpan<byte> payload)
        {
            payload = default;
            return false;
        }

        public int CountSpawns(int clientId)
            => Sent.Count(x => x.ClientId == clientId && x.Type == EReplicationPacketType.Spawn);

        public int CountDespawns(int clientId)
            => Sent.Count(x => x.ClientId == clientId && x.Type == EReplicationPacketType.Despawn);
    }

    /// <summary>大负载复制组件：内嵌大数组，序列化体积远超旧版固定栈缓冲（spawn 512B / update 128B）。</summary>
    private struct BigSyncTestComponent : IComponent
    {
        public int[]? Data;
    }

    /// <summary>BigSyncTestComponent 手写序列化器（[len][value]*）。</summary>
    private sealed class BigSyncTestSerializer : IComponentSerializer<BigSyncTestComponent>
    {
        public void Write(in BigSyncTestComponent c, ref ByteWriter w)
        {
            w.Write(c.Data!.Length);
            foreach (var v in c.Data!) w.Write(v);
        }

        public void Read(ref BigSyncTestComponent c, ref ByteReader r)
        {
            int len = r.ReadInt();
            c.Data = new int[len];
            for (int i = 0; i < len; i++) c.Data[i] = r.ReadInt();
        }
    }

    /// <summary>BigSyncTestComponent 手写 diff（引用相等判定变更——测试中通过换新数组触发 update）。</summary>
    private readonly struct BigSyncTestDiff : IReplicationDiff<BigSyncTestComponent>
    {
        public bool Equals(in BigSyncTestComponent a, in BigSyncTestComponent b) => ReferenceEquals(a.Data, b.Data);
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

    [Fact]
    public void NonReplicatedEntity_HasInvalidNetworkId()
    {
        var world = new World(ENetMode.DedicatedServer);
        var server = new ReplicationServer(world.Store, new NullServerTransport());
        var entity = world.Store.CreateEntity();

        Assert.False(server.GetNetworkId(entity.Id).IsValid);
    }

    [Fact]
    public void OwnerFilteredEntity_SpawnsOnlyForOwnerClient()
    {
        SerializerRegistry.Register(new SyncTestSerializer());
        ReplicationRegistry.Register<SyncTestComponent>(new SyncTestDiff());

        var world = new World(ENetMode.DedicatedServer);
        var transport = new RecordingTransport();
        var server = new ReplicationServer(world.Store, transport);
        EntityLifecycle.Subscribe(world, server.HandleLifecycle);
        server.AddClient(0);
        server.AddClient(1);
        server.Tick();   // 首帧空全量快照，清 NeedsSnapshot

        // 加入之后创建的实体 → 增量 Spawn，按 Owner 只进客户端 1 的 Bubble
        var entity = world.Store.CreateEntity();
        entity.AddComponent(new OwnerComponent { PlayerId = 1 });
        entity.AddComponent(new SyncTestComponent { Value = 1 });
        server.Tick();

        Assert.Equal(0, transport.CountSpawns(0));
        Assert.Equal(1, transport.CountSpawns(1));
    }

    [Fact]
    public void RemoveClient_StopsReceivingSpawns()
    {
        SerializerRegistry.Register(new SyncTestSerializer());
        ReplicationRegistry.Register<SyncTestComponent>(new SyncTestDiff());

        var world = new World(ENetMode.DedicatedServer);
        var transport = new RecordingTransport();
        var server = new ReplicationServer(world.Store, transport);
        EntityLifecycle.Subscribe(world, server.HandleLifecycle);
        server.AddClient(0);
        server.Tick();   // 首帧空全量快照，清 NeedsSnapshot

        var e1 = world.Store.CreateEntity();
        e1.AddComponent(new SyncTestComponent { Value = 1 });
        server.Tick();
        Assert.Equal(1, transport.CountSpawns(0));

        server.RemoveClient(0);
        var e2 = world.Store.CreateEntity();
        e2.AddComponent(new SyncTestComponent { Value = 2 });
        server.Tick();
        Assert.Equal(1, transport.CountSpawns(0));
    }

    [Fact]
    public void Diff_FreshThenCleanThenDirty()
    {
        SerializerRegistry.Register(new SyncTestSerializer());
        ReplicationRegistry.Register<SyncTestComponent>(new SyncTestDiff());

        var store = new EntityStore();
        var entity = store.CreateEntity();
        entity.AddComponent(new SyncTestComponent { Value = 1 });

        var entry = ReplicationRegistry.GetByComponentType(
            EntityStore.GetEntitySchema().GetComponentType<SyncTestComponent>())!;
        var shadowStore = entry.CreateShadowStore();
        var deltas = new List<ReplicationDelta>();

        // 1. 新实体（无 shadow）→ 恰好一个 dirty delta
        entry.Diff(shadowStore, store, deltas);
        Assert.Single(deltas);
        Assert.Equal(entity.Id, deltas[0].Entity.Id);
        Assert.Equal(entry.TypeId, deltas[0].TypeId);

        // 2. 无变更 → 零 delta
        deltas.Clear();
        entry.Diff(shadowStore, store, deltas);
        Assert.Empty(deltas);

        // 3. 变更组件（ref）→ 一个 delta，且 Entity/TypeId 正确
        ref var comp = ref entity.GetComponent<SyncTestComponent>();
        comp.Value = 2;
        deltas.Clear();
        entry.Diff(shadowStore, store, deltas);
        Assert.Single(deltas);
        Assert.Equal(entity.Id, deltas[0].Entity.Id);
        Assert.Equal(entry.TypeId, deltas[0].TypeId);
    }

    [Fact]
    public void ShadowStore_RemoveEntity_Prunes()
    {
        var shadow = new ShadowStore<SyncTestComponent>();
        shadow.ByEntityId[42] = new SyncTestComponent { Value = 5 };

        ((IShadowStore)shadow).RemoveEntity(42);

        Assert.False(shadow.ByEntityId.ContainsKey(42));
    }

    [Fact]
    public void DeleteEntity_PrunesShadow_RespawnsOnIdReuse()
    {
        SerializerRegistry.Register(new SyncTestSerializer());
        ReplicationRegistry.Register<SyncTestComponent>(new SyncTestDiff());

        var world = new World(ENetMode.DedicatedServer);
        var transport = new RecordingTransport();
        var server = new ReplicationServer(world.Store, transport);
        EntityLifecycle.Subscribe(world, server.HandleLifecycle);
        server.AddClient(0);
        server.Tick();                                   // 首帧空全量快照，清 NeedsSnapshot

        var entity = world.Store.CreateEntity();
        entity.AddComponent(new SyncTestComponent { Value = 5 });
        server.Tick();                                   // 增量 spawn
        Assert.Equal(1, transport.CountSpawns(0));

        entity.DeleteEntity();                           // → despawn 只发给含该实体的客户端 + shadow 清理
        Assert.Equal(1, transport.CountDespawns(0));

        var recreated = world.Store.CreateEntity();      // Friflo 复用被删实体的 id
        Assert.Equal(entity.Id, recreated.Id);
        recreated.AddComponent(new SyncTestComponent { Value = 5 });

        server.Tick();                                   // 若 shadow 未清理 → 新实体被误判"已有 shadow"→ 不 spawn
        Assert.Equal(2, transport.CountSpawns(0));
    }

    [Fact]
    public void LargeComponent_SpawnAndUpdate_DoNotOverflowFixedBuffers()
    {
        SerializerRegistry.Register(new BigSyncTestSerializer());
        ReplicationRegistry.Register<BigSyncTestComponent>(new BigSyncTestDiff());

        var world = new World(ENetMode.DedicatedServer);
        var transport = new RecordingTransport();
        var server = new ReplicationServer(world.Store, transport);
        EntityLifecycle.Subscribe(world, server.HandleLifecycle);
        server.AddClient(0);
        server.Tick();   // 首帧空全量快照，清 NeedsSnapshot

        // 组件负载 512B + 包帧头 ≈ 529B → 超过旧版固定栈缓冲（spawn 512B / update 128B）
        var big = new int[128];
        for (int i = 0; i < big.Length; i++) big[i] = i;
        var entity = world.Store.CreateEntity();
        entity.AddComponent(new BigSyncTestComponent { Data = big });
        server.Tick();   // 修复前：SendSpawn 固定 stackalloc[512] 溢出 → ArgumentOutOfRangeException 崩溃

        Assert.Equal(1, transport.CountSpawns(0));

        // 换新大数组（新引用 → diff 判定变更 → 增量 update），负载仍超旧版固定缓冲
        ref var comp = ref entity.GetComponent<BigSyncTestComponent>();
        comp.Data = new int[128];
        server.Tick();   // 修复前：SendUpdate 固定 stackalloc[128] 溢出 → ArgumentOutOfRangeException 崩溃

        Assert.Contains(transport.Sent, x => x.ClientId == 0 && x.Type == EReplicationPacketType.Update);
    }

    [Fact]
    public void LateJoinManyEntities_FullSnapshot_DoesNotOverflow()
    {
        SerializerRegistry.Register(new SyncTestSerializer());
        ReplicationRegistry.Register<SyncTestComponent>(new SyncTestDiff());

        var world = new World(ENetMode.DedicatedServer);
        var transport = new RecordingTransport();
        var server = new ReplicationServer(world.Store, transport);
        EntityLifecycle.Subscribe(world, server.HandleLifecycle);

        // 先创建足够多的实体（全量快照远超固定栈缓冲），再晚加入 → 走 ArrayPool 翻倍扩容路径
        for (int i = 0; i < 200; i++)
        {
            var entity = world.Store.CreateEntity();
            entity.AddComponent(new SyncTestComponent { Value = i });
        }

        server.AddClient(0);
        server.Tick();   // 修复前：固定 stackalloc[2048] 溢出 → ArgumentOutOfRangeException 崩溃

        Assert.Single(transport.Sent, x => x.ClientId == 0 && x.Type == EReplicationPacketType.FullSnapshot);
    }

    [Fact]
    public void Dispose_ReturnsRentedArrayPoolBuffers()
    {
        SerializerRegistry.Register(new SyncTestSerializer());
        ReplicationRegistry.Register<SyncTestComponent>(new SyncTestDiff());

        var world = new World(ENetMode.DedicatedServer);
        var server = new ReplicationServer(world.Store, new NullServerTransport());
        EntityLifecycle.Subscribe(world, server.HandleLifecycle);
        server.AddClient(0);
        server.Tick();   // 首帧空全量快照 → 租借 snapshotBuffer

        var entity = world.Store.CreateEntity();
        entity.AddComponent(new SyncTestComponent { Value = 1 });
        server.Tick();   // 增量 Spawn → 租借 spawnBuffer

        ref var comp = ref entity.GetComponent<SyncTestComponent>();
        comp.Value = 2;
        server.Tick();   // dirty → SendUpdate → 租借 updateBuffer

        Assert.True(GetBuffer(server, "snapshotBuffer").Length > 0);
        Assert.True(GetBuffer(server, "spawnBuffer").Length > 0);
        Assert.True(GetBuffer(server, "updateBuffer").Length > 0);

        server.Dispose();

        Assert.Empty(GetBuffer(server, "snapshotBuffer"));
        Assert.Empty(GetBuffer(server, "spawnBuffer"));
        Assert.Empty(GetBuffer(server, "updateBuffer"));
    }

    private static byte[] GetBuffer(ReplicationServer server, string fieldName)
        => (byte[])(typeof(ReplicationServer)
            .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(server)!);
}
