using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;

namespace Gameplay.Abilities;

/// <summary>
/// 属性聚合管理器（POCO，非 ECS System）。
/// 管理 (Entity, GameplayAttribute) → AttributeAggregator 的映射、脏队列、固定阶段 Flush。
/// 不再依赖 DirtyAttributeComponent 或 QuerySystem。
/// </summary>
public class AttributeAggregatorManager
{
    // ── 核心数据 ──
    private readonly Dictionary<AttributeKey, AttributeAggregator> aggregators = new();
    private readonly Dictionary<int, GameplayAttribute> registeredAttributes = new();

    // ── 反向索引 ──
    private readonly Dictionary<Entity, List<AttributeKey>> entityToAttributes = new();
    private readonly Dictionary<int, List<AttributeKey>> handleToAttributes = new();

    // ── 脏队列（双缓冲） ──
    private List<AttributeKey> currentDirtyQueue = new();
    private List<AttributeKey> nextDirtyQueue = new();
    private bool isFlushing;

    // ── Attribute 注册 ──

    /// <summary>注册 GameplayAttribute 句柄。SG 通过 RegisterAll 批量调用。ID 冲突时抛出异常。</summary>
    public void RegisterAttribute(GameplayAttribute attribute)
    {
        if (registeredAttributes.ContainsKey(attribute.Id))
            throw new InvalidOperationException($"GameplayAttribute ID {attribute.Id} 重复注册——SG 应保证唯一性");
        registeredAttributes[attribute.Id] = attribute;
    }

    /// <summary>尝试解析 Handle 为完整 GameplayAttribute。未注册时返回 false。</summary>
    private bool TryResolve(GameplayAttributeHandle handle, out GameplayAttribute attr)
        => registeredAttributes.TryGetValue(handle.Id, out attr);

    // ── 公开查询 API ──

    /// <summary>返回上次 Flush 后的已结算 CurrentValue。无 aggregator 时返回 Component BaseValue。</summary>
    public float GetCurrentValue(Entity entity, GameplayAttributeHandle handle)
    {
        var key = new AttributeKey(entity, handle);
        if (aggregators.TryGetValue(key, out var agg))
        {
            if (TryResolve(handle, out var attr) && attr.TryReadCurrentValue(entity, out var cv))
                return cv;
            return 0f;
        }
        // 无 aggregator → 返回 BaseValue
        if (TryResolve(handle, out var attr2) && attr2.TryReadBaseValue(entity, out var bv))
            return bv;
        return 0f;
    }

    /// <summary>返回 aggregator 的 BaseValue。不存在时读 Component BaseValue。</summary>
    public float GetBaseValue(Entity entity, GameplayAttributeHandle handle)
    {
        var key = new AttributeKey(entity, handle);
        if (aggregators.TryGetValue(key, out var agg))
            return agg.BaseValue;
        if (TryResolve(handle, out var attr) && attr.TryReadBaseValue(entity, out var baseValue))
            return baseValue;
        return 0f;
    }

    /// <summary>是否存在 (Entity, Attribute) 的聚合器。</summary>
    public bool HasAggregator(Entity entity, GameplayAttributeHandle handle)
        => aggregators.ContainsKey(new AttributeKey(entity, handle));

    // ── BaseValue 统一入口 ──

    /// <summary>设置 BaseValue——唯一写入入口。值改变时 MarkDirty。</summary>
    public void SetBaseValue(Entity entity, GameplayAttributeHandle handle, float value)
    {
        var key = new AttributeKey(entity, handle);
        if (!aggregators.TryGetValue(key, out var agg))
        {
            agg = CreateAggregator(entity, handle, key);
        }
        if (agg.SetBaseValue(value))
            MarkDirty(key, agg);
    }

    // ── Aggregator 修改 API ──

    /// <summary>设置聚合器的 Mod 源值。首次创建时从 Component 读取 BaseValue 初始化。</summary>
    public void SetAggregatorValue(Entity entity, GameplayAttributeHandle handle, float sourceValue)
    {
        var key = new AttributeKey(entity, handle);
        if (!aggregators.TryGetValue(key, out var agg))
        {
            agg = CreateAggregator(entity, handle, key);
        }
        if (agg.SetBaseValue(sourceValue))
            MarkDirty(key, agg);
    }

    /// <summary>为 (Entity, Attribute) 添加 GE Modifier。</summary>
    public void AddAggregatorMod(Entity entity, GameplayAttributeHandle handle, int geHandle,
        float magnitude, EGameplayModOp op)
    {
        var key = new AttributeKey(entity, handle);
        if (!aggregators.TryGetValue(key, out var agg))
        {
            agg = CreateAggregator(entity, handle, key);
        }
        if (agg.AddMod(geHandle, magnitude, op))
        {
            MarkDirty(key, agg);
            AddHandleToAttributeIndex(geHandle, key);
        }
    }

    /// <summary>移除指定 GE Handle 的所有 Modifier。仅实际删除的 aggregator 才 MarkDirty。</summary>
    public void RemoveAggregatorModsByHandle(int handle)
    {
        if (handleToAttributes.TryGetValue(handle, out var keys))
        {
            foreach (var key in keys)
            {
                if (aggregators.TryGetValue(key, out var agg))
                {
                    if (agg.RemoveModsByHandle(handle))
                        MarkDirty(key, agg);
                }
            }
            handleToAttributes.Remove(handle);
        }
    }

    // ── Flush ──

    /// <summary>
    /// 固定阶段批量刷新：遍历当前脏队列，Evaluate 并写回 CurrentValue。
    /// Flush 中新增的 Dirty 进入 nextDirtyQueue，留待下一帧处理。
    /// </summary>
    public void Flush()
    {
        isFlushing = true;

        for (int i = 0; i < currentDirtyQueue.Count; i++)
        {
            var key = currentDirtyQueue[i];
            if (!aggregators.TryGetValue(key, out var agg))
                continue;

            float result = agg.Evaluate();
            agg.Dirty = false;

            if (TryResolve(key.Attribute, out var attr))
            {
                attr.TryWriteCurrentValue(key.Entity, result);
            }
        }

        isFlushing = false;
        currentDirtyQueue.Clear();

        // 交换队列：Flush 期间的新增 Dirty 下一帧处理
        Swap(ref currentDirtyQueue, ref nextDirtyQueue);
    }

    // ── int 兼容重载（平滑迁移期，不走 Resolve）──

    public float GetCurrentValue(Entity entity, int attributeId)
    {
        var key = new AttributeKey(entity, new GameplayAttributeHandle(attributeId));
        return aggregators.TryGetValue(key, out var agg) ? agg.Evaluate() : 0f;
    }

    public float GetBaseValue(Entity entity, int attributeId)
    {
        var key = new AttributeKey(entity, new GameplayAttributeHandle(attributeId));
        return aggregators.TryGetValue(key, out var agg) ? agg.BaseValue : 0f;
    }

    public bool HasAggregator(Entity entity, int attributeId)
        => aggregators.ContainsKey(new AttributeKey(entity, new GameplayAttributeHandle(attributeId)));

    public void SetAggregatorValue(Entity entity, int attributeId, float sourceValue)
    {
        var handle = new GameplayAttributeHandle(attributeId);
        var key = new AttributeKey(entity, handle);
        if (!aggregators.TryGetValue(key, out var agg))
        {
            agg = new AttributeAggregator();
            aggregators[key] = agg;
        }
        if (agg.SetBaseValue(sourceValue))
            MarkDirty(key, agg);
    }

    public void AddAggregatorMod(Entity entity, int attributeId, int geHandle,
        float magnitude, EGameplayModOp op)
    {
        var handle = new GameplayAttributeHandle(attributeId);
        var key = new AttributeKey(entity, handle);
        if (!aggregators.TryGetValue(key, out var agg))
        {
            agg = new AttributeAggregator();
            aggregators[key] = agg;
        }
        if (agg.AddMod(geHandle, magnitude, op))
        {
            MarkDirty(key, agg);
            AddHandleToAttributeIndex(geHandle, key);
        }
    }

    // ── Entity 生命周期 ──

    /// <summary>清理指定 Entity 的所有 aggregators 和排队项。谁删 Entity 谁负责调此方法。</summary>
    public void RemoveEntity(Entity entity)
    {
        if (entityToAttributes.TryGetValue(entity, out var keys))
        {
            foreach (var key in keys)
            {
                aggregators.Remove(key);
                // 同时清理 handleToAttributes 引用（懒清理——下次 RemoveAggregatorModsByHandle 跳过不存在 key）
                currentDirtyQueue.Remove(key);
                nextDirtyQueue.Remove(key);
            }
            entityToAttributes.Remove(entity);
        }
    }

    // ── 内部方法 ──

    private void MarkDirty(AttributeKey key, AttributeAggregator agg)
    {
        if (agg.Dirty) return;
        agg.Dirty = true;

        if (isFlushing)
            nextDirtyQueue.Add(key);
        else
            currentDirtyQueue.Add(key);
    }

    private AttributeAggregator CreateAggregator(Entity entity, GameplayAttributeHandle handle,
        AttributeKey key)
    {
        var agg = new AttributeAggregator();
        // 从 Component 读取已有 BaseValue 初始化
        if (TryResolve(handle, out var attr) && attr.TryReadBaseValue(entity, out var baseValue))
            agg.SetBaseValue(baseValue);
        aggregators[key] = agg;
        AddEntityToAttributeIndex(entity, key);
        return agg;
    }

    private void AddEntityToAttributeIndex(Entity entity, AttributeKey key)
    {
        if (!entityToAttributes.TryGetValue(entity, out var list))
        {
            list = new List<AttributeKey>();
            entityToAttributes[entity] = list;
        }
        list.Add(key);
    }

    private void AddHandleToAttributeIndex(int handle, AttributeKey key)
    {
        if (!handleToAttributes.TryGetValue(handle, out var list))
        {
            list = new List<AttributeKey>();
            handleToAttributes[handle] = list;
        }
        if (!list.Contains(key))
            list.Add(key);
    }

    private static void Swap<T>(ref List<T> a, ref List<T> b)
    {
        (a, b) = (b, a);
    }
}
