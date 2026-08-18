using System;
using Gameplay.Core;
using Xunit;

namespace Gameplay.Tests.Core;

public class SerializationTests
{
    [Fact]
    public void ByteWriter_WriteIntThenFloat_Roundtrips()
    {
        Span<byte> buf = stackalloc byte[64];
        var writer = new ByteWriter(buf);
        writer.Write(42);
        writer.Write(3.5f);

        var reader = new ByteReader(buf[..writer.BytesWritten]);
        Assert.Equal(42, reader.ReadInt());
        Assert.Equal(3.5f, reader.ReadFloat(), 4);
    }

    [Fact]
    public void ByteWriter_WriteVector3_Roundtrips()
    {
        Span<byte> buf = stackalloc byte[64];
        var writer = new ByteWriter(buf);
        var v = new Vector3(1f, 2f, 3f);
        writer.Write(in v);

        var reader = new ByteReader(buf[..writer.BytesWritten]);
        var got = reader.ReadVector3();
        Assert.Equal(v, got);
    }

    [Fact]
    public void EntitySnapshot_CaptureAndApply_Roundtrips()
    {
        var serializer = new HealthComponentSerializer();
        SerializerRegistry.Register(serializer);

        var world = new World(ENetMode.Standalone);
        var entity = world.Store.CreateEntity();
        entity.AddComponent(new HealthComponent { Current = 75f, Max = 100f, IsAlive = true });

        Span<byte> buf = stackalloc byte[256];
        var writer = new ByteWriter(buf);
        EntitySnapshot.Capture(entity, ref writer);
        // 修改原组件
        ref var health = ref entity.GetComponent<HealthComponent>();
        health.Current = 10f;

        var reader = new ByteReader(buf[..writer.BytesWritten]);
        EntitySnapshot.Apply(entity, ref reader);

        Assert.Equal(75f, entity.GetComponent<HealthComponent>().Current);
    }

    private sealed class HealthComponentSerializer : IComponentSerializer<HealthComponent>
    {
        public void Write(in HealthComponent c, ref ByteWriter w)
        {
            w.Write(c.Current);
            w.Write(c.Max);
            w.Write(c.IsAlive);
        }
        public void Read(ref HealthComponent c, ref ByteReader r)
        {
            c.Current = r.ReadFloat();
            c.Max = r.ReadFloat();
            c.IsAlive = r.ReadBool();
        }
    }
}
