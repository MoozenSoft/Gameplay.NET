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

    [Fact]
    public void EntitySnapshot_Apply_UnknownTypeId_Throws()
    {
        // 手动构造含未知 typeId 的 buffer：[count=1][typeId=999]，无需 payload（Apply 应在读取数据前 fail-fast）
        Span<byte> buf = stackalloc byte[16];
        var writer = new ByteWriter(buf);
        writer.Write(1);    // count
        writer.Write(999);  // 未知 typeId

        var reader = new ByteReader(buf[..writer.BytesWritten]);
        var world = new World(ENetMode.Standalone);
        var entity = world.Store.CreateEntity();

        System.IO.InvalidDataException? caught = null;
        try { EntitySnapshot.Apply(entity, ref reader); }
        catch (System.IO.InvalidDataException ex) { caught = ex; }

        Assert.NotNull(caught);
    }

    [Fact]
    public void SerializerRegistry_RegisterSameTypeTwice_DoesNotDoubleWrite()
    {
        var serializer = new HealthComponentSerializer();
        SerializerRegistry.Register(serializer);
        SerializerRegistry.Register(serializer);   // 重复注册应替换（保留原 TypeId）而非追加

        var world = new World(ENetMode.Standalone);
        var entity = world.Store.CreateEntity();
        entity.AddComponent(new HealthComponent { Current = 75f, Max = 100f, IsAlive = true });

        Span<byte> buf = stackalloc byte[256];
        var writer = new ByteWriter(buf);
        EntitySnapshot.Capture(entity, ref writer);

        var reader = new ByteReader(buf[..writer.BytesWritten]);
        Assert.Equal(1, reader.ReadInt());   // 组件只写一遍
    }

    [Fact]
    public void EntitySnapshot_Capture_WritesStableTypeId()
    {
        SerializerRegistry.Register(new HealthComponentSerializer());

        var world = new World(ENetMode.Standalone);
        var entity = world.Store.CreateEntity();
        entity.AddComponent(new HealthComponent { Current = 1f, Max = 2f, IsAlive = true });

        Span<byte> buf = stackalloc byte[256];
        var writer = new ByteWriter(buf);
        EntitySnapshot.Capture(entity, ref writer);

        var reader = new ByteReader(buf[..writer.BytesWritten]);
        Assert.Equal(1, reader.ReadInt());                    // count
        Assert.Equal(1932327161, reader.ReadInt());           // FNV-1a("Gameplay.Core.HealthComponent")，锁死 schema
    }

    [Fact]
    public void EntitySnapshot_Capture_OrdersByStableTypeId()
    {
        SerializerRegistry.Register(new HealthComponentSerializer());        // uint 1932327161
        SerializerRegistry.Register(new TransformComponentSerializer());     // uint 2343681079

        var world = new World(ENetMode.Standalone);
        var entity = world.Store.CreateEntity();
        entity.AddComponent(new TransformComponent { Scale = 1f });
        entity.AddComponent(new HealthComponent { Current = 1f, Max = 2f, IsAlive = true });

        Span<byte> buf = stackalloc byte[256];
        var writer = new ByteWriter(buf);
        EntitySnapshot.Capture(entity, ref writer);

        var reader = new ByteReader(buf[..writer.BytesWritten]);
        Assert.Equal(2, reader.ReadInt());                    // count
        Assert.Equal(1932327161, reader.ReadInt());           // HealthComponent（uint 更小，排前）
        reader.ReadFloat(); reader.ReadFloat(); reader.ReadBool();  // 跳过 Health 载荷（Current/Max/IsAlive）
        Assert.Equal(-1951286217, reader.ReadInt());          // TransformComponent（int 表示负值）
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

    private sealed class TransformComponentSerializer : IComponentSerializer<TransformComponent>
    {
        public void Write(in TransformComponent c, ref ByteWriter w) => w.Write(c.Scale);
        public void Read(ref TransformComponent c, ref ByteReader r) => c.Scale = r.ReadFloat();
    }
}
