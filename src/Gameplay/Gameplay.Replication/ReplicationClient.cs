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
        var seen = new HashSet<NetworkId>();
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
            seen.Add(id);
        }

        // 删多余：快照未涵盖的本地镜像 → 服务端已不存在 → 删除镜像实体并移除映射
        var stale = new List<KeyValuePair<NetworkId, Entity>>();
        foreach (var kv in mirror)
            if (!seen.Contains(kv.Key))
                stale.Add(kv);
        foreach (var kv in stale)
        {
            mirror.Remove(kv.Key);
            if (!kv.Value.IsNull) kv.Value.DeleteEntity();
        }
    }
}
