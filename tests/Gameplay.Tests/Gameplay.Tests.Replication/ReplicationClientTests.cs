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
