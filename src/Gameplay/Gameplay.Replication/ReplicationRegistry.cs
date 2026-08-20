using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Gameplay.Core;

namespace Gameplay.Replication;

/// <summary>复制集注册中心（static，类型级，跨 World 共享）——装配「序列化器 + diff」。</summary>
public static class ReplicationRegistry
{
    private static readonly Dictionary<int, IReplicationEntry> byTypeId = new();
    private static readonly Dictionary<ComponentType, IReplicationEntry> byComponentType = new();
    private static readonly List<IReplicationEntry> entries = new();

    /// <summary>注册复制组件（须先在 SerializerRegistry 注册序列化器，否则 fail-fast）。幂等：重复注册替换而非追加。</summary>
    public static void Register<T>(IReplicationDiff<T> diff) where T : struct, IComponent
    {
        var type = typeof(T);
        var serializer = SerializerRegistry.Get<T>()
            ?? throw new InvalidOperationException($"组件 {type.FullName} 未注册序列化器，无法复制（先 SerializerRegistry.Register）");
        int typeId = SerializerRegistry.ComputeTypeId(type);
        var componentType = EntityStore.GetEntitySchema().GetComponentType<T>();
        if (byTypeId.ContainsKey(typeId))
        {
            // 重复注册：替换条目保留原 TypeId（对齐 SerializerRegistry.Register 的幂等语义）
            var replacement = new ReplicationEntry<T>(typeId, componentType, serializer, diff);
            byTypeId[typeId] = replacement;
            byComponentType[componentType] = replacement;
            for (int i = 0; i < entries.Count; i++)
                if (entries[i].TypeId == typeId) entries[i] = replacement;
            return;
        }
        var entry = new ReplicationEntry<T>(typeId, componentType, serializer, diff);
        byTypeId[typeId] = entry;
        byComponentType[componentType] = entry;
        entries.Add(entry);
    }

    internal static IReplicationEntry? GetEntry(int typeId)
        => byTypeId.TryGetValue(typeId, out var e) ? e : null;

    internal static IReplicationEntry? GetByComponentType(ComponentType type)
        => byComponentType.TryGetValue(type, out var e) ? e : null;

    internal static IReadOnlyList<IReplicationEntry> Entries => entries;
}

/// <summary>非泛型复制条目（EntitySnapshot 式统一编解码 + shadow-diff）。</summary>
internal interface IReplicationEntry
{
    int TypeId { get; }
    ComponentType ComponentType { get; }
    bool HasComponent(Entity entity);
    void Capture(Entity entity, ref ByteWriter writer);   // 只写组件数据（不含 typeId）
    void Apply(Entity entity, ref ByteReader reader);      // 只读组件数据
    IShadowStore CreateShadowStore();
    void Diff(IShadowStore shadowStore, EntityStore store, List<ReplicationDelta> output);
}

/// <summary>shadow 状态（per-World 实例）。</summary>
internal interface IShadowStore
{
    /// <summary>实体删除时清理 shadow——防 id 复用后新实体被误判为"已有 shadow"而跳过 spawn。</summary>
    void RemoveEntity(int entityId);
}

internal sealed class ShadowStore<T> : IShadowStore where T : struct, IComponent
{
    public readonly Dictionary<int, T> ByEntityId = new();

    public void RemoveEntity(int entityId) => ByEntityId.Remove(entityId);
}

/// <summary>泛型适配器——IComponentSerializer&lt;T&gt; + IReplicationDiff&lt;T&gt; → IReplicationEntry。</summary>
internal sealed class ReplicationEntry<T> : IReplicationEntry where T : struct, IComponent
{
    private readonly IComponentSerializer<T> serializer;
    private readonly IReplicationDiff<T> diff;

    public int TypeId { get; }
    public ComponentType ComponentType { get; }

    public ReplicationEntry(int typeId, ComponentType componentType, IComponentSerializer<T> serializer, IReplicationDiff<T> diff)
    {
        TypeId = typeId;
        ComponentType = componentType;
        this.serializer = serializer;
        this.diff = diff;
    }

    public bool HasComponent(Entity entity) => entity.HasComponent<T>();

    public void Capture(Entity entity, ref ByteWriter writer)
    {
        ref var c = ref entity.GetComponent<T>();
        serializer.Write(in c, ref writer);
    }

    public void Apply(Entity entity, ref ByteReader reader)
    {
        if (!entity.HasComponent<T>())
            entity.AddComponent<T>();
        ref var c = ref entity.GetComponent<T>();
        serializer.Read(ref c, ref reader);
    }

    public IShadowStore CreateShadowStore() => new ShadowStore<T>();

    public void Diff(IShadowStore shadowStore, EntityStore store, List<ReplicationDelta> output)
    {
        var shadows = (ShadowStore<T>)shadowStore;
        store.Query<T>().ForEachEntity((ref T component, Entity entity) =>
        {
            if (shadows.ByEntityId.TryGetValue(entity.Id, out var shadow))
            {
                if (!diff.Equals(in component, in shadow))
                {
                    output.Add(new ReplicationDelta(entity, TypeId));
                    shadows.ByEntityId[entity.Id] = component;
                }
            }
            else
            {
                output.Add(new ReplicationDelta(entity, TypeId));   // 新实体 → 视为 dirty
                shadows.ByEntityId[entity.Id] = component;
            }
        });
    }
}
