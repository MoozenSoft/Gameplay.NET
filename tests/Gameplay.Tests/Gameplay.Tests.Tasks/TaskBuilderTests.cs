// tests/Gameplay.Tests/Gameplay.Tests.Abilities/AbilityTask/WaitDelayTaskTests.cs
namespace Gameplay.Tests.Tasks;

using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Gameplay.Abilities;
using Gameplay.Tasks;
using Gameplay.Tags;
using Xunit;

public class TaskBuilderTests
{
    private static readonly GameplayTag TestTag = CreateTestTag();
    private static readonly GameplayTag TestTag2 = CreateTestTag2();

    // Request 是只读查找，必须先注册（不依赖其他测试类的静态构造——xUnit 并行执行）
    private static GameplayTag CreateTestTag()
    {
        GameplayTagManager.RegisterTags("Test.Tag");
        return GameplayTag.Request("Test.Tag");
    }

    private static GameplayTag CreateTestTag2()
    {
        GameplayTagManager.RegisterTags("Test.Tag2");
        return GameplayTag.Request("Test.Tag2");
    }

    private static GameplayTagContainer RequiredQuery => new GameplayTagContainer { TestTag, TestTag2 };

    private static Entity CreateWaitDelayTask(EntityStore store, float duration, Entity activeAbility)
        => TaskBuilder.Delay(store, duration, activeAbility);

    [Fact]
    public void WaitDelayTask_HasAllRequiredComponents()
    {
        var store = new EntityStore();
        var activeAbility = store.CreateEntity();
        var entity = CreateWaitDelayTask(store, 1f, activeAbility);

        Assert.True(entity.HasComponent<TaskStateComponent>());
        Assert.True(entity.HasComponent<TaskOwnerComponent>());
        Assert.True(entity.HasComponent<DelayComponent>());

        ref var ownerComp = ref entity.GetComponent<TaskOwnerComponent>();
        Assert.Equal(activeAbility.Id, ownerComp.Owner.Id);
        // Builder 把 Task 挂到 Owner（ActiveAbility）下，供 AllTasksDone 检测
        Assert.Equal(activeAbility.Id, entity.Parent.Id);
    }

    [Fact]
    public void WaitTagQueryAdded_CreatesQueryListener()
    {
        var store = new EntityStore();
        var owner = store.CreateEntity();
        var target = store.CreateEntity();

        var entity = TaskBuilder.WaitTagQueryAdded(store, target, RequiredQuery, owner);

        Assert.True(entity.HasComponent<TaskStateComponent>());
        Assert.True(entity.HasComponent<TaskOwnerComponent>());
        ref var listener = ref entity.GetComponent<TagListenerComponent>();
        Assert.Equal(target.Id, listener.Target.Id);
        Assert.NotNull(listener.RequiredTags);
        Assert.Equal(2, listener.RequiredTags.Count); // 防御性拷贝：内容一致
        Assert.Equal(TagCondition.Added, listener.Condition);
    }

    [Fact]
    public void WaitTagQueryRemoved_CreatesQueryListener()
    {
        var store = new EntityStore();
        var owner = store.CreateEntity();
        var target = store.CreateEntity();

        var entity = TaskBuilder.WaitTagQueryRemoved(store, target, RequiredQuery, owner);

        ref var listener = ref entity.GetComponent<TagListenerComponent>();
        Assert.Equal(TagCondition.Removed, listener.Condition);
        Assert.NotNull(listener.RequiredTags);
    }

    [Fact]
    public void WaitTagQueryAdded_EmptyContainer_Throws()
    {
        var store = new EntityStore();
        var owner = store.CreateEntity();
        var target = store.CreateEntity();

        // 空集合的 HasAll 恒真/恒假，语义无意义——Builder fail-fast
        Assert.Throws<System.ArgumentException>(() =>
            TaskBuilder.WaitTagQueryAdded(store, target, new GameplayTagContainer(), owner));
    }

    [Fact]
    public void WaitTagQueryAdded_ContainerMutation_DoesNotAffectTask()
    {
        var store = new EntityStore();
        var owner = store.CreateEntity();
        var target = store.CreateEntity();
        var required = RequiredQuery;

        var entity = TaskBuilder.WaitTagQueryAdded(store, target, required, owner);

        // 防御性拷贝：调用者修改原容器不影响已创建 Task 的条件
        required.RemoveTag(TestTag);
        ref var listener = ref entity.GetComponent<TagListenerComponent>();
        Assert.Equal(2, listener.RequiredTags.Count);
    }

    [Fact]
    public void WaitEffectApplied_CreatesEffectListener()
    {
        var store = new EntityStore();
        var owner = store.CreateEntity();
        var target = store.CreateEntity();

        var entity = TaskBuilder.WaitEffectApplied(store, target,
            GameplayEffectQuery.MakeQuery_MatchAnyGrantedTags(RequiredQuery), owner);

        ref var listener = ref entity.GetComponent<EffectListener>();
        Assert.Equal(target.Id, listener.Target.Id);
        Assert.NotNull(listener.Query);
        Assert.Equal(EEffectCondition.Applied, listener.Condition);
    }

    [Fact]
    public void WaitEffectRemoved_CreatesEffectListener()
    {
        var store = new EntityStore();
        var owner = store.CreateEntity();
        var target = store.CreateEntity();

        var entity = TaskBuilder.WaitEffectRemoved(store, target,
            GameplayEffectQuery.MakeQuery_MatchAnyGrantedTags(RequiredQuery), owner);

        ref var listener = ref entity.GetComponent<EffectListener>();
        Assert.Equal(EEffectCondition.Removed, listener.Condition);
        Assert.NotNull(listener.Query);
    }

    [Fact]
    public void WaitAbilityActivate_CreatesListener_WithTagCopy()
    {
        var store = new EntityStore();
        var owner = store.CreateEntity();
        var abilityTags = RequiredQuery;

        var entity = TaskBuilder.WaitAbilityActivate(store, abilityTags, character: store.CreateEntity(), owner);

        ref var listener = ref entity.GetComponent<AbilityActivateListener>();
        Assert.NotNull(listener.AbilityTags);
        Assert.Equal(2, listener.AbilityTags.Count); // 防御性拷贝

        // 调用者修改原容器不影响已创建 Task
        abilityTags.RemoveTag(TestTag);
        Assert.Equal(2, listener.AbilityTags.Count);
    }

    [Fact]
    public void WaitAbilityActivate_NullTags_MatchesAny()
    {
        var store = new EntityStore();
        var owner = store.CreateEntity();

        var entity = TaskBuilder.WaitAbilityActivate(store, null, character: store.CreateEntity(), owner);

        ref var listener = ref entity.GetComponent<AbilityActivateListener>();
        Assert.Null(listener.AbilityTags);
    }

    [Fact]
    public void WaitInputPress_CreatesPressListener()
    {
        var store = new EntityStore();
        var owner = store.CreateEntity();

        var entity = TaskBuilder.WaitInputPress(store, 1, owner);

        ref var listener = ref entity.GetComponent<InputListener>();
        Assert.Equal(1, listener.ActionId);
        Assert.Equal(EInputTrigger.Press, listener.Trigger);
    }

    [Fact]
    public void WaitInputRelease_CreatesReleaseListener()
    {
        var store = new EntityStore();
        var owner = store.CreateEntity();

        var entity = TaskBuilder.WaitInputRelease(store, 1, owner);

        ref var listener = ref entity.GetComponent<InputListener>();
        Assert.Equal(1, listener.ActionId);
        Assert.Equal(EInputTrigger.Release, listener.Trigger);
    }

    [Fact]
    public void WaitInputHeld_CreatesHoldListener()
    {
        var store = new EntityStore();
        var owner = store.CreateEntity();

        var entity = TaskBuilder.WaitInputHeld(store, 1, owner);

        ref var listener = ref entity.GetComponent<InputListener>();
        Assert.Equal(1, listener.ActionId);
        Assert.Equal(EInputTrigger.Hold, listener.Trigger);
    }

    [Fact]
    public void MoveTo_CreatesMoveComponent()
    {
        var store = new EntityStore();
        var owner = store.CreateEntity();
        var target = store.CreateEntity();

        var entity = TaskBuilder.MoveTo(store, target, new Position(5f, 0f, 0f), 2f, owner);

        ref var move = ref entity.GetComponent<MoveToComponent>();
        Assert.Equal(target.Id, move.Target.Id);
        Assert.Equal(new Position(5f, 0f, 0f), move.Destination);
        Assert.Equal(2f, move.Duration);
    }

    [Fact]
    public void Repeat_CreatesTimerComponent()
    {
        var store = new EntityStore();
        var owner = store.CreateEntity();

        var entity = TaskBuilder.Repeat(store, interval: 0.5f, count: 3, pulseEventId: 42, owner);

        ref var timer = ref entity.GetComponent<TimerComponent>();
        Assert.Equal(0.5f, timer.Interval);
        Assert.Equal(3, timer.RemainingPulses);
        Assert.Equal((ushort)42, timer.PulseEventId);
    }

    [Fact]
    public void Repeat_InvalidInterval_Throws()
    {
        var store = new EntityStore();
        var owner = store.CreateEntity();

        Assert.Throws<System.ArgumentException>(() => TaskBuilder.Repeat(store, 0f, 3, 42, owner));
    }

    [Fact]
    public void Repeat_InvalidCount_Throws()
    {
        var store = new EntityStore();
        var owner = store.CreateEntity();

        Assert.Throws<System.ArgumentException>(() => TaskBuilder.Repeat(store, 0.5f, 0, 42, owner));
    }

    [Fact]
    public void WaitAttributeAbove_CreatesThresholdListener()
    {
        var store = new EntityStore();
        var owner = store.CreateEntity();
        var target = store.CreateEntity();

        var entity = TaskBuilder.WaitAttributeAbove(store, target, new GameplayAttribute(0), 100f, owner);

        ref var listener = ref entity.GetComponent<AttributeListener>();
        Assert.Equal(EAttributeCondition.Above, listener.Condition);
        Assert.Equal(100f, listener.Threshold);
    }

    [Fact]
    public void WaitAttributeBelow_CreatesThresholdListener()
    {
        var store = new EntityStore();
        var owner = store.CreateEntity();
        var target = store.CreateEntity();

        var entity = TaskBuilder.WaitAttributeBelow(store, target, new GameplayAttribute(0), 50f, owner);

        ref var listener = ref entity.GetComponent<AttributeListener>();
        Assert.Equal(EAttributeCondition.Below, listener.Condition);
        Assert.Equal(50f, listener.Threshold);
    }

    [Fact]
    public void WaitAttributeRatioAbove_CreatesRatioListener()
    {
        var store = new EntityStore();
        var owner = store.CreateEntity();
        var target = store.CreateEntity();

        var entity = TaskBuilder.WaitAttributeRatioAbove(store, target, new GameplayAttribute(0), 0.5f, owner);

        ref var listener = ref entity.GetComponent<AttributeListener>();
        Assert.Equal(EAttributeCondition.RatioAbove, listener.Condition);
        Assert.Equal(0.5f, listener.Threshold);
    }

    [Fact]
    public void WaitAttributeRatioBelow_CreatesRatioListener()
    {
        var store = new EntityStore();
        var owner = store.CreateEntity();
        var target = store.CreateEntity();

        var entity = TaskBuilder.WaitAttributeRatioBelow(store, target, new GameplayAttribute(0), 0.5f, owner);

        ref var listener = ref entity.GetComponent<AttributeListener>();
        Assert.Equal(EAttributeCondition.RatioBelow, listener.Condition);
        Assert.Equal(0.5f, listener.Threshold);
    }

    [Fact]
    public void WaitDelayTask_CompletesAfterDuration()
    {
        var store = new EntityStore();
        var activeAbility = store.CreateEntity();
        activeAbility.AddComponent(new ActiveAbilityComponent
        {
            Handle = new AbilityHandle(1),
            IsActive = true,
            Owner = store.CreateEntity(),
            State = EAbilityInstanceState.Active,
        });

        var taskEntity = CreateWaitDelayTask(store, 0.3f, activeAbility);

        var root = new SystemRoot(store)
        {
            new DelaySystem(),
        };

        root.Update(new UpdateTick(0.16f, 0)); // Pending → Running (Elapsed still 0)
        root.Update(new UpdateTick(0.16f, 0)); // Running → Elapsed=0.16
        root.Update(new UpdateTick(0.16f, 0)); // Running → Elapsed=0.32 >= 0.3 → Done

        ref var state = ref taskEntity.GetComponent<TaskStateComponent>();
        Assert.Equal(ETaskState.Done, state.State);
    }

    [Fact]
    public void WaitDelayTask_ZeroDuration_CompletesInOneFrame()
    {
        var store = new EntityStore();
        var activeAbility = store.CreateEntity();
        activeAbility.AddComponent(new ActiveAbilityComponent
        {
            Handle = new AbilityHandle(1),
            IsActive = true,
            Owner = store.CreateEntity(),
            State = EAbilityInstanceState.Active,
        });

        var taskEntity = CreateWaitDelayTask(store, 0f, activeAbility);

        var root = new SystemRoot(store)
        {
            new DelaySystem(),
        };

        root.Update(new UpdateTick(0.16f, 0));

        ref var state = ref taskEntity.GetComponent<TaskStateComponent>();
        Assert.Equal(ETaskState.Done, state.State);
    }
}
