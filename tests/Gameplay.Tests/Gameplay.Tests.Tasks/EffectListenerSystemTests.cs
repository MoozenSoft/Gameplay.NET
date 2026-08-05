// tests/Gameplay.Tests/Gameplay.Tests.Tasks/EffectListenerSystemTests.cs
namespace Gameplay.Tests.Tasks;

using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Gameplay.Abilities;
using Gameplay.Tasks;
using Gameplay.Tags;
using Xunit;

/// <summary>对应 src/Gameplay/Gameplay.Tasks/Systems/EffectListenerSystem.cs——事件驱动：GE 施加/移除 → Task 完成。</summary>
public class EffectListenerSystemTests
{
    private static readonly GameplayTag TestTag = CreateTestTag();

    private static GameplayTag CreateTestTag()
    {
        GameplayTagManager.RegisterTags("Test.Effect");
        return GameplayTag.Request("Test.Effect");
    }

    private static (Entity Target, Entity Task, EffectSystem EffectSys, SystemRoot Root) Setup(EEffectCondition condition)
    {
        var store = new EntityStore();
        var mgr = new AttributeAggregatorManager();
        var effectSys = new EffectSystem(mgr);
        var target = store.CreateEntity();

        var task = TaskBuilder.WaitEffectApplied(store, target,
            GameplayEffectQuery.MakeQuery_MatchAnyGrantedTags(new GameplayTagContainer { TestTag }),
            owner: store.CreateEntity());
        task.GetComponent<EffectListener>().Condition = condition;

        var root = new SystemRoot(store) { new EffectListenerSystem(effectSys, store) };
        return (target, task, effectSys, root);
    }

    private static GameplayEffectSpec CreateMatchingSpec(float duration = 5f)
    {
        var ge = new GameplayEffect
        {
            GrantedTags = new GameplayTagContainer { TestTag },
        };
        return new GameplayEffectSpec(ge, 1f) { Duration = duration };
    }

    private static GameplayEffectSpec CreateNonMatchingSpec()
    {
        var ge = new GameplayEffect();
        return new GameplayEffectSpec(ge, 1f) { Duration = 5f };
    }

    private static ETaskState GetState(Entity task)
        => task.GetComponent<TaskStateComponent>().State;

    [Fact]
    public void Applied_MatchingEffect_CompletesTask()
    {
        var (target, task, effectSys, root) = Setup(EEffectCondition.Applied);

        root.Update(new UpdateTick(0.16f, 0)); // Pending → Running（注册事件监听）

        effectSys.Apply(CreateMatchingSpec(), target);

        Assert.Equal(ETaskState.Done, GetState(task));
    }

    [Fact]
    public void Applied_NonMatchingEffect_DoesNotComplete()
    {
        var (target, task, effectSys, root) = Setup(EEffectCondition.Applied);

        root.Update(new UpdateTick(0.16f, 0)); // Pending → Running

        effectSys.Apply(CreateNonMatchingSpec(), target);

        Assert.Equal(ETaskState.Running, GetState(task));
    }

    [Fact]
    public void Applied_EffectOnOtherTarget_DoesNotComplete()
    {
        var (target, task, effectSys, root) = Setup(EEffectCondition.Applied);
        var otherTarget = target.Store.CreateEntity();

        root.Update(new UpdateTick(0.16f, 0)); // Pending → Running

        effectSys.Apply(CreateMatchingSpec(), otherTarget);

        Assert.Equal(ETaskState.Running, GetState(task));
    }

    [Fact]
    public void Removed_MatchingEffect_CompletesTask()
    {
        var (target, task, effectSys, root) = Setup(EEffectCondition.Removed);

        var handle = effectSys.Apply(CreateMatchingSpec(), target);

        root.Update(new UpdateTick(0.16f, 0)); // Pending → Running
        Assert.Equal(ETaskState.Running, GetState(task));

        effectSys.RemoveEffect(handle, EEffectEndType.Normal);

        Assert.Equal(ETaskState.Done, GetState(task));
    }

    [Fact]
    public void Removed_NonMatchingEffect_DoesNotComplete()
    {
        var (target, task, effectSys, root) = Setup(EEffectCondition.Removed);

        var handle = effectSys.Apply(CreateNonMatchingSpec(), target);

        root.Update(new UpdateTick(0.16f, 0)); // Pending → Running

        effectSys.RemoveEffect(handle, EEffectEndType.Normal);

        Assert.Equal(ETaskState.Running, GetState(task));
    }

    [Fact]
    public void TargetDeleted_CompletesTask()
    {
        var (target, task, effectSys, root) = Setup(EEffectCondition.Applied);

        root.Update(new UpdateTick(0.16f, 0)); // Pending → Running（注册事件监听）
        Assert.Equal(ETaskState.Running, GetState(task));

        target.DeleteEntity(); // 级联删除不发 EffectRemoved——防御性完成
        root.Update(new UpdateTick(0.16f, 0));

        Assert.Equal(ETaskState.Done, GetState(task));
    }

    [Fact]
    public void PendingTask_IgnoresEventsBeforeRunning()
    {
        var (target, task, effectSys, root) = Setup(EEffectCondition.Applied);

        // 事件发生在 Task 注册（Running）之前 → 不被处理（事件驱动注册语义）
        effectSys.Apply(CreateMatchingSpec(), target);
        Assert.Equal(ETaskState.Pending, GetState(task));

        root.Update(new UpdateTick(0.16f, 0)); // Pending → Running
        Assert.Equal(ETaskState.Running, GetState(task));
    }
}
