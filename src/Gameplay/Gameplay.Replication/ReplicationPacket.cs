using System;
using System.Collections.Generic;
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

    private static readonly int[] singleTypeId = new int[1];   // WriteSingleComponent 共享单元素缓冲（0 GC）

    /// <summary>写单组件负载（复用 WriteComponents 容器格式）：[count=1][typeId][data]。</summary>
    public static void WriteSingleComponent(Entity entity, int typeId, ref ByteWriter writer)
    {
        singleTypeId[0] = typeId;
        WriteComponents(entity, singleTypeId, ref writer);
    }

    /// <summary>写 Spawn（组件全量）：NetworkId + [count][typeId+data]*。</summary>
    public static void WriteSpawn(Entity entity, NetworkId id, ref ByteWriter writer)
    {
        WriteType(EReplicationPacketType.Spawn, ref writer);
        WriteNetworkId(id, ref writer);
        var ids = GatherReplicatedTypeIds(entity);
        WriteComponents(entity, ids, ref writer);
    }

    /// <summary>写全量快照包（某 Bubble 全量）：Type=FullSnapshot + [count]{NetworkId + [count][typeId+data]*}*。</summary>
    public static void WriteFullSnapshot(IReadOnlyList<(NetworkId Id, Entity Entity)> entries, ref ByteWriter writer)
    {
        WriteType(EReplicationPacketType.FullSnapshot, ref writer);
        writer.Write(entries.Count);
        for (int i = 0; i < entries.Count; i++)
        {
            var (id, entity) = entries[i];
            WriteNetworkId(id, ref writer);
            WriteComponents(entity, GatherReplicatedTypeIds(entity), ref writer);
        }
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
