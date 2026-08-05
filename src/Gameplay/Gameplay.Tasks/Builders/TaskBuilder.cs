using System;
using Friflo.Engine.ECS;
using Gameplay.Abilities;
using Gameplay.Tags;

namespace Gameplay.Tasks;

/// <summary>
/// Task Builder（Facade/Factory）——创建 Task Entity 的唯一入口。<br/>
/// 本身无逻辑：创建 TaskState + TaskOwner + 能力组件（Archetype 组合），并挂到 Owner 下。<br/>
/// 使用 Friflo <see cref="EntityStoreExtensions.CreateEntity{T1,T2,T3}"/> 重载，
/// 创建即最终 Archetype——避免逐个 AddComponent 的多次 structural change（Archetype 迁移）。<br/>
/// 底层不存在 GameplayTask / AbilityTask 之分——只有不同的 Owner 与 Component 组合。<br/>
/// Ability 用 TaskBuilder.WaitXxx（owner = ActiveAbility），Gameplay 用 TaskBuilder.Delay / TaskBuilder.MoveTo（owner = 任意 Entity）。
/// </summary>
public static class TaskBuilder
{
    /// <summary>创建延时 Task——等待指定时间后完成（Done）。</summary>
    public static Entity Delay(EntityStore store, float duration, Entity owner, int taskHandle = 0)
    {
        var entity = store.CreateEntity(
            new TaskStateComponent { State = ETaskState.Pending },
            new TaskOwnerComponent { Owner = owner, TaskHandle = taskHandle },
            new DelayComponent { Duration = duration, Elapsed = 0f });
        owner.AddChild(entity);
        return entity;
    }

    /// <summary>创建事件监听 Task——收到匹配的 GameplayEvent 时完成（Done）。</summary>
    public static Entity WaitEvent(EntityStore store, ushort eventId, Entity owner, int taskHandle = 0)
    {
        var entity = store.CreateEntity(
            new TaskStateComponent { State = ETaskState.Pending },
            new TaskOwnerComponent { Owner = owner, TaskHandle = taskHandle },
            new GameplayEventListener { EventId = eventId });
        owner.AddChild(entity);
        return entity;
    }

    /// <summary>创建 Tag 监听 Task——目标获得指定 Tag 时完成（Done）。</summary>
    public static Entity WaitTagAdded(EntityStore store, Entity target, GameplayTag tag, Entity owner, int taskHandle = 0)
    {
        var entity = CreateTagListenerTask(store, target, tag, null, TagCondition.Added, owner, taskHandle);
        return entity;
    }

    /// <summary>创建 Tag 监听 Task——目标移除指定 Tag 时完成（Done）。</summary>
    public static Entity WaitTagRemoved(EntityStore store, Entity target, GameplayTag tag, Entity owner, int taskHandle = 0)
    {
        var entity = CreateTagListenerTask(store, target, tag, null, TagCondition.Removed, owner, taskHandle);
        return entity;
    }

    /// <summary>创建 Tag Query 监听 Task——目标获得指定 Tag 集合（全部）时完成（Done）。</summary>
    public static Entity WaitTagQueryAdded(EntityStore store, Entity target, GameplayTagContainer required, Entity owner, int taskHandle = 0)
    {
        var entity = CreateTagListenerTask(store, target, default, required, TagCondition.Added, owner, taskHandle);
        return entity;
    }

    /// <summary>创建 Tag Query 监听 Task——目标失去指定 Tag 集合中的任一 Tag（条件被破坏）时完成（Done）。</summary>
    public static Entity WaitTagQueryRemoved(EntityStore store, Entity target, GameplayTagContainer required, Entity owner, int taskHandle = 0)
    {
        var entity = CreateTagListenerTask(store, target, default, required, TagCondition.Removed, owner, taskHandle);
        return entity;
    }

    /// <summary>创建属性监听 Task——目标属性变化指定次数后完成（Done）。</summary>
    public static Entity WaitAttributeChange(EntityStore store, Entity target, GameplayAttribute attribute, int count, Entity owner, int taskHandle = 0)
    {
        var entity = CreateAttributeTask(store, target, attribute, EAttributeCondition.Changed, 0f, count, owner, taskHandle);
        return entity;
    }

    /// <summary>创建属性阈值 Task——目标属性 CurrentValue 超过阈值时完成（Done）。</summary>
    public static Entity WaitAttributeAbove(EntityStore store, Entity target, GameplayAttribute attribute, float threshold, Entity owner, int taskHandle = 0)
    {
        var entity = CreateAttributeTask(store, target, attribute, EAttributeCondition.Above, threshold, 0, owner, taskHandle);
        return entity;
    }

    /// <summary>创建属性阈值 Task——目标属性 CurrentValue 低于阈值时完成（Done）。</summary>
    public static Entity WaitAttributeBelow(EntityStore store, Entity target, GameplayAttribute attribute, float threshold, Entity owner, int taskHandle = 0)
    {
        var entity = CreateAttributeTask(store, target, attribute, EAttributeCondition.Below, threshold, 0, owner, taskHandle);
        return entity;
    }

    /// <summary>创建属性比值阈值 Task——目标属性 CurrentValue/BaseValue 超过阈值时完成（Done）。</summary>
    public static Entity WaitAttributeRatioAbove(EntityStore store, Entity target, GameplayAttribute attribute, float threshold, Entity owner, int taskHandle = 0)
    {
        var entity = CreateAttributeTask(store, target, attribute, EAttributeCondition.RatioAbove, threshold, 0, owner, taskHandle);
        return entity;
    }

    /// <summary>创建属性比值阈值 Task——目标属性 CurrentValue/BaseValue 低于阈值时完成（Done）。</summary>
    public static Entity WaitAttributeRatioBelow(EntityStore store, Entity target, GameplayAttribute attribute, float threshold, Entity owner, int taskHandle = 0)
    {
        var entity = CreateAttributeTask(store, target, attribute, EAttributeCondition.RatioBelow, threshold, 0, owner, taskHandle);
        return entity;
    }

    /// <summary>创建 GE 监听 Task——匹配的 GE 施加到目标时完成（Done）。</summary>
    public static Entity WaitEffectApplied(EntityStore store, Entity target, GameplayEffectQuery query, Entity owner, int taskHandle = 0)
    {
        var entity = CreateEffectTask(store, target, query, EEffectCondition.Applied, owner, taskHandle);
        return entity;
    }

    /// <summary>创建 GE 监听 Task——匹配的 GE 从目标移除时完成（Done）。</summary>
    public static Entity WaitEffectRemoved(EntityStore store, Entity target, GameplayEffectQuery query, Entity owner, int taskHandle = 0)
    {
        var entity = CreateEffectTask(store, target, query, EEffectCondition.Removed, owner, taskHandle);
        return entity;
    }

    /// <summary>创建 Ability 激活监听 Task——character 激活匹配 AssetTags 的 Ability 时完成（Done）。character 无符号 = 任何角色；abilityTags 为 null/空 = 任何 Ability。</summary>
    public static Entity WaitAbilityActivate(EntityStore store, GameplayTagContainer? abilityTags, Entity character, Entity owner, int taskHandle = 0)
    {
        var entity = store.CreateEntity(
            new TaskStateComponent { State = ETaskState.Pending },
            new TaskOwnerComponent { Owner = owner, TaskHandle = taskHandle },
            new AbilityActivateListener
            {
                Character = character,
                // 防御性拷贝——可变 class 引用，防调用者后续修改改写所有 Task 的条件
                AbilityTags = abilityTags == null ? null : CopyContainer(abilityTags),
            });
        owner.AddChild(entity);
        return entity;
    }

    /// <summary>创建输入监听 Task——指定动作本帧按下时完成（Done）。</summary>
    public static Entity WaitInputPress(EntityStore store, int actionId, Entity owner, int taskHandle = 0)
    {
        var entity = CreateInputTask(store, actionId, EInputTrigger.Press, owner, taskHandle);
        return entity;
    }

    /// <summary>创建输入监听 Task——指定动作本帧释放时完成（Done）。</summary>
    public static Entity WaitInputRelease(EntityStore store, int actionId, Entity owner, int taskHandle = 0)
    {
        var entity = CreateInputTask(store, actionId, EInputTrigger.Release, owner, taskHandle);
        return entity;
    }

    /// <summary>创建输入监听 Task——指定动作处于按住状态时完成（Done）。</summary>
    public static Entity WaitInputHeld(EntityStore store, int actionId, Entity owner, int taskHandle = 0)
    {
        var entity = CreateInputTask(store, actionId, EInputTrigger.Hold, owner, taskHandle);
        return entity;
    }

    /// <summary>创建重复 Task——每 interval 秒发一次 pulseEventId 事件（Timer 能力），count 次后完成（Done）。</summary>
    public static Entity Repeat(EntityStore store, float interval, int count, ushort pulseEventId, Entity owner, int taskHandle = 0)
    {
        if (interval <= 0f)
            throw new ArgumentException("Interval 必须大于 0——否则无限脉冲风暴", nameof(interval));
        if (count <= 0)
            throw new ArgumentException("Count 必须大于 0——无限脉冲请直接使用 TimerComponent.RemainingPulses=0", nameof(count));

        var entity = store.CreateEntity(
            new TaskStateComponent { State = ETaskState.Pending },
            new TaskOwnerComponent { Owner = owner, TaskHandle = taskHandle },
            new TimerComponent
            {
                Interval = interval,
                RemainingPulses = count,
                PulseEventId = pulseEventId,
            });
        owner.AddChild(entity);
        return entity;
    }

    /// <summary>创建移动 Task（Action）——在 duration 秒内将 target 从当前位置插值到目的地，完成后结束（Done）。</summary>
    public static Entity MoveTo(EntityStore store, Entity target, Position destination, float duration, Entity owner, int taskHandle = 0)
    {
        var entity = store.CreateEntity(
            new TaskStateComponent { State = ETaskState.Pending },
            new TaskOwnerComponent { Owner = owner, TaskHandle = taskHandle },
            new MoveToComponent { Target = target, Destination = destination, Duration = duration });
        owner.AddChild(entity);
        return entity;
    }

    /// <summary>创建生成 Task（Action）——克隆预制实体到指定位置，立即完成（Done）。生成结果存 SpawnRequestComponent.SpawnedEntity。</summary>
    public static Entity SpawnActor(EntityStore store, Entity prefab, Position spawnPosition, Entity owner, int taskHandle = 0)
    {
        var entity = store.CreateEntity(
            new TaskStateComponent { State = ETaskState.Pending },
            new TaskOwnerComponent { Owner = owner, TaskHandle = taskHandle },
            new SpawnRequestComponent { Prefab = prefab, SpawnPosition = spawnPosition });
        owner.AddChild(entity);
        return entity;
    }

    /// <summary>创建 Commit 阶段监听 Task——目标 ActiveAbility Commit 完成（State=Active）时完成（Done）。</summary>
    public static Entity WaitCommitPhase(EntityStore store, Entity activeAbility, Entity owner, int taskHandle = 0)
    {
        var entity = store.CreateEntity(
            new TaskStateComponent { State = ETaskState.Pending },
            new TaskOwnerComponent { Owner = owner, TaskHandle = taskHandle },
            new CommitPhaseListener { Target = activeAbility });
        owner.AddChild(entity);
        return entity;
    }

    /// <summary>创建属性监听 Task 基础 Archetype：TaskState + TaskOwner + AttributeListener（指定条件）。</summary>
    private static Entity CreateAttributeTask(EntityStore store, Entity target, GameplayAttribute attribute,
        EAttributeCondition condition, float threshold, int count, Entity owner, int taskHandle)
    {
        var entity = store.CreateEntity(
            new TaskStateComponent { State = ETaskState.Pending },
            new TaskOwnerComponent { Owner = owner, TaskHandle = taskHandle },
            new AttributeListener
            {
                Target = target,
                Attribute = attribute,
                Condition = condition,
                Threshold = threshold,
                Count = count,
            });
        owner.AddChild(entity);
        return entity;
    }

    /// <summary>
    /// 创建 Tag 监听 Task 基础 Archetype：TaskState + TaskOwner + TagListenerComponent。<br/>
    /// 单 Tag 模式（required 为 null）用 tag；Query 模式用 required（防御性拷贝——可变 class 引用，
    /// 防调用者后续修改改写所有 Task 的条件）。
    /// </summary>
    private static Entity CreateTagListenerTask(EntityStore store, Entity target, GameplayTag tag,
        GameplayTagContainer? required, TagCondition condition, Entity owner, int taskHandle)
    {
        if (required == null)
        {
            // fail-fast：无效 Tag（未注册名 → Request 返回 Invalid）会让条件永不满足，静默挂起
            if (!tag.IsValid)
                throw new ArgumentException("Tag 无效——未注册的 Tag 名会让条件永不满足", nameof(tag));
        }
        else if (required.Count == 0)
        {
            // 空集合的 HasAll 恒真/恒假，语义无意义
            throw new ArgumentException("RequiredTags 不能为空", nameof(required));
        }

        var entity = store.CreateEntity(
            new TaskStateComponent { State = ETaskState.Pending },
            new TaskOwnerComponent { Owner = owner, TaskHandle = taskHandle },
            new TagListenerComponent
            {
                Target = target,
                Tag = tag,
                RequiredTags = required == null ? null : CopyContainer(required),
                Condition = condition,
            });
        owner.AddChild(entity);
        return entity;
    }

    /// <summary>拷贝 GameplayTagContainer（冷路径，可接受分配）。容器内无效 Tag（未注册名）fail-fast——否则被静默跳过，条件意外变成"匹配任何"。</summary>
    private static GameplayTagContainer CopyContainer(GameplayTagContainer source)
    {
        var copy = new GameplayTagContainer();
        foreach (var tag in source)
        {
            if (!tag.IsValid)
                throw new ArgumentException("容器包含无效 Tag——未注册的 Tag 名会被静默忽略，导致条件变成'匹配任何'", nameof(source));
            copy.AddTag(tag);
        }
        return copy;
    }

    /// <summary>创建输入监听 Task 基础 Archetype：TaskState + TaskOwner + InputListener。</summary>
    private static Entity CreateInputTask(EntityStore store, int actionId, EInputTrigger trigger, Entity owner, int taskHandle)
    {
        var entity = store.CreateEntity(
            new TaskStateComponent { State = ETaskState.Pending },
            new TaskOwnerComponent { Owner = owner, TaskHandle = taskHandle },
            new InputListener { ActionId = actionId, Trigger = trigger });
        owner.AddChild(entity);
        return entity;
    }

    /// <summary>创建 GE 监听 Task 基础 Archetype：TaskState + TaskOwner + EffectListener（Query 防御性拷贝——快照语义）。</summary>
    private static Entity CreateEffectTask(EntityStore store, Entity target, GameplayEffectQuery query,
        EEffectCondition condition, Entity owner, int taskHandle)
    {
        if (query == null)
            throw new ArgumentException("Query 不能为 null——无法匹配任何 GE", nameof(query));

        var entity = store.CreateEntity(
            new TaskStateComponent { State = ETaskState.Pending },
            new TaskOwnerComponent { Owner = owner, TaskHandle = taskHandle },
            new EffectListener
            {
                Target = target,
                Query = CopyQuery(query),
                Condition = condition,
            });
        owner.AddChild(entity);
        return entity;
    }

    /// <summary>深拷贝 GameplayEffectQuery（可变 class 引用——快照语义，防调用者后续修改改写所有 Task 的条件）。</summary>
    private static GameplayEffectQuery CopyQuery(GameplayEffectQuery source)
    {
        return new GameplayEffectQuery
        {
            OwningTagQuery = source.OwningTagQuery.Count > 0 ? CopyContainer(source.OwningTagQuery) : new(),
            EffectTagQuery = source.EffectTagQuery.Count > 0 ? CopyContainer(source.EffectTagQuery) : new(),
            Definition = source.Definition,
        };
    }
}
