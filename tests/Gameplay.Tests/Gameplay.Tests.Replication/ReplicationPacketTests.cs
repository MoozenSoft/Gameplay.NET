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
