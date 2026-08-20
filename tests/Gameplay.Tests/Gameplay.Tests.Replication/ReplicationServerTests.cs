using System;
using System.Collections.Generic;
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

        var entity = world.Store.CreateEntity();
        entity.AddComponent(new SyncTestComponent { Value = 5 });
        server.Tick();                                   // 首次 spawn
        Assert.Equal(1, transport.CountSpawns(0));

        entity.DeleteEntity();                           // → despawn 广播 + shadow 清理
        Assert.Equal(1, transport.CountDespawns(0));

        var recreated = world.Store.CreateEntity();      // Friflo 复用被删实体的 id
        Assert.Equal(entity.Id, recreated.Id);
        recreated.AddComponent(new SyncTestComponent { Value = 5 });

        server.Tick();                                   // 若 shadow 未清理 → 新实体被误判"已有 shadow"→ 不 spawn
        Assert.Equal(2, transport.CountSpawns(0));
    }
}
