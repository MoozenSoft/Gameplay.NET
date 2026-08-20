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
        // 清理 shadow——Friflo 复用被删实体 id 时，新实体不会被误判为"已有 shadow"而跳过 spawn
        foreach (var shadowStore in shadowStores.Values)
            shadowStore.RemoveEntity(entity.Id);
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
