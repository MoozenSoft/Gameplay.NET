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
        Assert.NotNull(ReplicationRegistry.GetByComponentType(EntityStore.GetEntitySchema().GetComponentType<HealthComponent>()));
    }

    [Fact]
    public void GeneratedSerializer_Roundtrips()
    {
        ReplicatedComponentRegistration.RegisterAll();
        var store = new EntityStore();
        var entity = store.CreateEntity();
        entity.AddComponent(new HealthComponent { Current = 75f, Max = 100f, IsAlive = true });

        var entry = ReplicationRegistry.GetByComponentType(EntityStore.GetEntitySchema().GetComponentType<HealthComponent>())!;
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
