using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;

namespace Gameplay.Core;

/// <summary>组件序列化器注册中心（static，程序级唯一映射，自增 typeId 索引）。</summary>
public static class SerializerRegistry
{
    private static readonly List<ISnapshotEntry> Entries = new();
    private static readonly Dictionary<Type, ISnapshotEntry> ByType = new();

    public static void Register<T>(IComponentSerializer<T> serializer) where T : struct, IComponent
    {
        var entry = new SnapshotEntry<T>(Entries.Count + 1, serializer);
        Entries.Add(entry);
        ByType[typeof(T)] = entry;
    }

    public static IComponentSerializer<T>? Get<T>() where T : struct, IComponent
        => ByType.TryGetValue(typeof(T), out var box) ? ((SnapshotEntry<T>)box).Serializer : null;

    /// <summary>枚举已注册的快照条目（按注册顺序，typeId = index + 1）。</summary>
    internal static IReadOnlyList<ISnapshotEntry> EnumerateRegistered() => Entries;

    /// <summary>按 typeId 查询快照条目（typeId 从 1 开始）。</summary>
    internal static ISnapshotEntry? GetByTypeId(int typeId)
        => typeId >= 1 && typeId <= Entries.Count ? Entries[typeId - 1] : null;
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
