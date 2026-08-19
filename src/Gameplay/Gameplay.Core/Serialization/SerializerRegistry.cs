using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;

namespace Gameplay.Core;

/// <summary>组件序列化器注册中心（static，程序级唯一映射，typeId = FNV-1a 哈希 typeof(T).FullName，跨进程稳定）。</summary>
public static class SerializerRegistry
{
    private static readonly List<ISnapshotEntry> entries = new();             // 按 typeId（uint 序）升序
    private static readonly Dictionary<int, ISnapshotEntry> byId = new();     // typeId → entry
    private static readonly Dictionary<Type, ISnapshotEntry> byType = new();

    public static void Register<T>(IComponentSerializer<T> serializer) where T : struct, IComponent
    {
        var type = typeof(T);
        int typeId = ComputeTypeId(type);
        if (byType.TryGetValue(type, out var existing))
        {
            // 重复注册：替换条目保留原 TypeId（同类型 → 同哈希，天然相同）
            var replacement = new SnapshotEntry<T>(existing.TypeId, serializer);
            byId[existing.TypeId] = replacement;
            byType[type] = replacement;
            for (int i = 0; i < entries.Count; i++)
                if (entries[i].TypeId == existing.TypeId) entries[i] = replacement;
            return;
        }
        if (byId.TryGetValue(typeId, out _))
            throw new InvalidOperationException($"组件类型 {type.FullName} 的 typeId 哈希冲突：{typeId}（已存在其他组件使用同一 typeId）");
        var entry = new SnapshotEntry<T>(typeId, serializer);
        byType[type] = entry;
        byId[typeId] = entry;
        InsertSorted(entry);
    }

    /// <summary>FNV-1a 32-bit 标准常量：offset basis（初始种子）。</summary>
    private const uint FnvOffsetBasis = 2166136261;  // 0x811C9DC5
    /// <summary>FNV-1a 32-bit 标准常量：FNV prime（每轮乘数）。</summary>
    private const uint FnvPrime = 16777619;          // 0x01000193

    /// <summary>组件类型 → 稳定 typeId（FNV-1a 32-bit 哈希全名，跨进程一致、与注册顺序无关）。</summary>
    internal static int ComputeTypeId(Type type)
    {
        string name = type.FullName ?? type.Name;
        uint hash = FnvOffsetBasis;
        foreach (char c in name)
        {
            hash ^= c;
            hash *= FnvPrime;
        }
        return unchecked((int)hash);
    }

    public static IComponentSerializer<T>? Get<T>() where T : struct, IComponent
        => byType.TryGetValue(typeof(T), out var box) ? ((SnapshotEntry<T>)box).Serializer : null;

    /// <summary>枚举已注册的快照条目（按 typeId 升序，保证 Capture 字节流确定）。</summary>
    internal static IReadOnlyList<ISnapshotEntry> EnumerateRegistered() => entries;

    /// <summary>按 typeId 查询快照条目。</summary>
    internal static ISnapshotEntry? GetByTypeId(int typeId)
        => byId.TryGetValue(typeId, out var entry) ? entry : null;

    private static void InsertSorted(ISnapshotEntry entry)
    {
        int i = entries.Count;
        entries.Add(entry);
        while (i > 0 && ((uint)entries[i - 1].TypeId) > (uint)entry.TypeId)
        {
            entries[i] = entries[i - 1];
            i--;
        }
        entries[i] = entry;
    }
}

/// <summary>内部非泛型快照条目（EntitySnapshot 经由它按 typeId 统一编解码）。</summary>
internal interface ISnapshotEntry
{
    int TypeId { get; }
    bool HasComponent(Entity entity);
    void Capture(Entity entity, ref ByteWriter writer);
    void Apply(Entity entity, ref ByteReader reader);
}

/// <summary>泛型适配器——把 IComponentSerializer&lt;T&gt; 包装为非泛型 ISnapshotEntry。</summary>
internal sealed class SnapshotEntry<T> : ISnapshotEntry where T : struct, IComponent
{
    public int TypeId { get; }
    public IComponentSerializer<T> Serializer { get; }

    public SnapshotEntry(int typeId, IComponentSerializer<T> serializer) { TypeId = typeId; Serializer = serializer; }

    public bool HasComponent(Entity entity) => entity.HasComponent<T>();

    public void Capture(Entity entity, ref ByteWriter writer)
    {
        ref var c = ref entity.GetComponent<T>();
        Serializer.Write(in c, ref writer);
    }

    public void Apply(Entity entity, ref ByteReader reader)
    {
        if (!entity.HasComponent<T>())
            entity.AddComponent<T>();   // 快照里有但 entity 缺失 → 补上再读
        ref var c = ref entity.GetComponent<T>();
        Serializer.Read(ref c, ref reader);
    }
}
