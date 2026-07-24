// src/Gameplay/GameplayAbilities/Attribute/AttributeSystem.cs
using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;

namespace Gameplay.Abilities;

/// <summary>
/// 属性重算 System。只处理 DirtyBits 有标记的 Entity。
/// 每个 GameplayAttribute 对应一个 AttributeAggregator，由本 System 外部管理。
/// </summary>
public class AttributeSystem : QuerySystem<DirtyAttributeComponent>
{
    // (Entity.Id, attributeId) → Aggregator
    private readonly Dictionary<(int entityId, int attrId), AttributeAggregator> aggregators = new();

    // 反向索引（RealTime 用）：(sourceEntity.Id, sourceAttrId) → 受影响的 target Handle 列表
    private readonly Dictionary<(int entityId, int attrId), List<int>> realTimeReverseIndex = new();

    // AttributeId → CurrentValue 写回委托（SG 生成或手动注册，per-instance）
    private readonly Dictionary<int, Action<Entity, float>> currentValueWriters = new();

    /// <summary>注册 AttributeId 的 CurrentValue 写回委托。</summary>
    public void RegisterCurrentValueWriter(int attributeId, Action<Entity, float> writer)
        => currentValueWriters[attributeId] = writer;

    protected override void OnUpdate()
    {
        Query.ForEachEntity(
            (ref DirtyAttributeComponent dirty, Entity entity) =>
        {
            if (dirty.DirtyBits == 0) return;

            // 遍历所有 Set 的 bit
            ulong bits = dirty.DirtyBits;
            int attrId = 0;
            while (bits != 0)
            {
                if ((bits & 1) != 0)
                {
                    var key = (entity.Id, attrId);
                    if (aggregators.TryGetValue(key, out var agg))
                    {
                        float result = agg.Evaluate();
                        if (currentValueWriters.TryGetValue(attrId, out var writer))
                            writer(entity, result);
                    }
                }
                bits >>= 1;
                attrId++;
            }
            dirty.ClearAll();
        });
    }

    // ── Aggregator 管理（供 EffectSystem 调用） ──

    private static (int entityId, int attrId) Key(Entity e, int attrId) => (e.Id, attrId);

    public void SetAggregatorValue(Entity entity, int attributeId, float baseValue)
    {
        var key = Key(entity, attributeId);
        if (!aggregators.TryGetValue(key, out var agg))
        {
            agg = new AttributeAggregator();
            aggregators[key] = agg;
        }
        agg.BaseValue = baseValue;
    }

    public void AddAggregatorMod(Entity entity, int attributeId, int handle,
        float magnitude, EGameplayModOp op)
    {
        var key = Key(entity, attributeId);
        if (aggregators.TryGetValue(key, out var agg))
            agg.AddMod(handle, magnitude, op);
    }

    public void RemoveAggregatorModsByHandle(int handle)
    {
        foreach (var agg in aggregators.Values)
            agg.RemoveModsByHandle(handle);
    }

    public float GetCurrentValue(Entity entity, int attributeId)
    {
        var key = Key(entity, attributeId);
        if (aggregators.TryGetValue(key, out var agg))
            return agg.Evaluate();
        return 0f;
    }

    public bool HasAggregator(Entity entity, int attributeId)
        => aggregators.ContainsKey(Key(entity, attributeId));

    public float GetBaseValue(Entity entity, int attributeId)
    {
        var key = Key(entity, attributeId);
        if (aggregators.TryGetValue(key, out var agg))
            return agg.BaseValue;
        return 0f;
    }

    // ── RealTime 反向索引 ──

    public void RegisterRealTimeDependency(int sourceEntityId, int sourceAttrId, int targetHandle)
    {
        var key = (sourceEntityId, sourceAttrId);
        if (!realTimeReverseIndex.TryGetValue(key, out var list))
        {
            list = new List<int>();
            realTimeReverseIndex[key] = list;
        }
        list.Add(targetHandle);
    }

    public void UnregisterRealTimeDependencies(int handle)
    {
        foreach (var list in realTimeReverseIndex.Values)
            list.RemoveAll(h => h == handle);
    }
}
