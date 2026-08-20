using System;
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
            () => ReplicationRegistry.Register<SyncTestNoSerializerComponent>(new SyncTestNoSerializerDiff()));
    }

    [Fact]
    public void Register_ThenCaptureApply_Roundtrips()
    {
        SerializerRegistry.Register(new SyncTestSerializer());
        ReplicationRegistry.Register<SyncTestComponent>(new SyncTestDiff());

        var store = new EntityStore();
        var entity = store.CreateEntity();
        entity.AddComponent(new SyncTestComponent { Value = 77 });

        var entry = ReplicationRegistry.GetByComponentType(EntityStore.GetEntitySchema().GetComponentType<SyncTestComponent>());
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
