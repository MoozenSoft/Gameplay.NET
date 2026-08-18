using Friflo.Engine.ECS;

namespace Gameplay.Core;

/// <summary>实体快照（组件编解码，不含网络）。</summary>
public static class EntitySnapshot
{
    /// <summary>捕获实体已注册组件到 buffer：写 [count][typeId+数据]*，Apply 按头读，组件集变化不错位。</summary>
    public static void Capture(Entity entity, ref ByteWriter writer)
    {
        var entries = SerializerRegistry.EnumerateRegistered();
        int count = 0;
        foreach (var entry in entries)
            if (entry.HasComponent(entity)) count++;
        writer.Write(count);
        foreach (var entry in entries)
        {
            if (!entry.HasComponent(entity)) continue;
            writer.Write(entry.TypeId);
            entry.Capture(entity, ref writer);
        }
    }

    public static void Apply(Entity entity, ref ByteReader reader)
    {
        int count = reader.ReadInt();
        for (int i = 0; i < count; i++)
        {
            int typeId = reader.ReadInt();
            var entry = SerializerRegistry.GetByTypeId(typeId);
            if (entry != null) entry.Apply(entity, ref reader);
        }
    }
}
