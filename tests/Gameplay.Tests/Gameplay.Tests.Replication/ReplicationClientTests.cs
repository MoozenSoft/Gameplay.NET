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

    [Fact]
    public void FullSnapshot_ReconcilesStaleMirror()
    {
        SerializerRegistry.Register(new SyncTestSerializer());
        ReplicationRegistry.Register<SyncTestComponent>(new SyncTestDiff());

        var client = new ReplicationClient(new EntityStore(), new NullClientTransport());

        // 预置两个镜像：present(1) 与 stale(2，服务端已不存在)
        var sourceStore = new EntityStore();
        var presentSource = sourceStore.CreateEntity();
        presentSource.AddComponent(new SyncTestComponent { Value = 11 });
        var staleSource = sourceStore.CreateEntity();
        staleSource.AddComponent(new SyncTestComponent { Value = 22 });

        Span<byte> spawnBuf = stackalloc byte[128];
        var spawnWriter = new ByteWriter(spawnBuf);
        ReplicationPacket.WriteSpawn(presentSource, new NetworkId(1), ref spawnWriter);
        client.ApplyServerPacket(spawnBuf[..spawnWriter.BytesWritten]);

        spawnWriter = new ByteWriter(spawnBuf);
        ReplicationPacket.WriteSpawn(staleSource, new NetworkId(2), ref spawnWriter);
        client.ApplyServerPacket(spawnBuf[..spawnWriter.BytesWritten]);

        Assert.False(client.GetMirror(new NetworkId(2)).IsNull);   // stale 镜像存在

        // 服务端更新 present 组件值，构造只含 present(1) 的 FullSnapshot
        ref var comp = ref presentSource.GetComponent<SyncTestComponent>();
        comp.Value = 99;

        Span<byte> snapBuf = stackalloc byte[128];
        var writer = new ByteWriter(snapBuf);
        ReplicationPacket.WriteType(EReplicationPacketType.FullSnapshot, ref writer);
        writer.Write(1);   // 快照实体数
        ReplicationPacket.WriteNetworkId(new NetworkId(1), ref writer);
        ReplicationPacket.WriteComponents(presentSource, new[] { SerializerRegistry.ComputeTypeId(typeof(SyncTestComponent)) }, ref writer);

        client.ApplyServerPacket(snapBuf[..writer.BytesWritten]);

        // present 镜像被更新
        var present = client.GetMirror(new NetworkId(1));
        Assert.False(present.IsNull);
        Assert.Equal(99, present.GetComponent<SyncTestComponent>().Value);
        // stale 镜像被删除（快照未涵盖 → 本地多余）
        Assert.True(client.GetMirror(new NetworkId(2)).IsNull);
    }
}
