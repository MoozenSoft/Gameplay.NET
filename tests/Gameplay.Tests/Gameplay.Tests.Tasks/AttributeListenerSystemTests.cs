// tests/Gameplay.Tests/Gameplay.Tests.Tasks/AttributeListenerSystemTests.cs
namespace Gameplay.Tests.Tasks;

using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Gameplay.Abilities;
using Gameplay.Tasks;
using Xunit;

/// <summary>对应 src/Gameplay/Gameplay.Tasks/Systems/AttributeListenerSystem.cs。</summary>
public class AttributeListenerSystemTests
{
    private static readonly GameplayAttribute TestAttr = new GameplayAttribute(0);

    private static (Entity Target, Entity Task, AttributeAggregatorManager Mgr, SystemRoot Root) Setup(
        EAttributeCondition condition = EAttributeCondition.Changed, float threshold = 0f, int count = 0)
    {
        var store = new EntityStore();
        var mgr = new AttributeAggregatorManager();
        var target = store.CreateEntity();

        var task = store.CreateEntity();
        task.AddComponent(new TaskStateComponent { State = ETaskState.Pending });
        task.AddComponent(new AttributeListener
        {
            Target = target,
            Attribute = TestAttr,
            Condition = condition,
            Threshold = threshold,
            Count = count,
        });

        var root = new SystemRoot(store) { new AttributeListenerSystem(mgr) };
        return (target, task, mgr, root);
    }

    private static ETaskState GetState(Entity task)
        => task.GetComponent<TaskStateComponent>().State;

    [Fact]
    public void ValueChange_CompletesTask()
    {
        var (target, task, mgr, root) = Setup(count: 1, condition: EAttributeCondition.Changed);

        mgr.SetAggregatorValue(target, TestAttr, 100f);
        mgr.Flush(); // 已结算值 = 100

        root.Update(new UpdateTick(0.16f, 0)); // Pending → Running（快照 LastValue=100）
        Assert.Equal(ETaskState.Running, GetState(task));

        mgr.AddAggregatorMod(target, TestAttr, new GameplayEffectHandle(1), 20f, EGameplayModOp.Additive);
        mgr.Flush(); // 已结算值 = 120
        root.Update(new UpdateTick(0.16f, 0)); // 120 != 100 → Count-- → Done

        Assert.Equal(ETaskState.Done, GetState(task));
    }

    [Fact]
    public void InitialValue_DoesNotTriggerImmediateComplete()
    {
        var (target, task, mgr, root) = Setup(count: 1, condition: EAttributeCondition.Changed);

        // 目标属性初始值非 0——不构成"变化"，Task 不应立即完成
        mgr.SetAggregatorValue(target, TestAttr, 100f);
        mgr.Flush();

        root.Update(new UpdateTick(0.16f, 0)); // Pending → Running（快照）
        root.Update(new UpdateTick(0.16f, 0)); // 值未变 → 不 Done

        Assert.Equal(ETaskState.Running, GetState(task));
    }

    [Fact]
    public void MultipleChanges_CompletesAfterRequestedCount()
    {
        var (target, task, mgr, root) = Setup(count: 2, condition: EAttributeCondition.Changed);

        mgr.SetAggregatorValue(target, TestAttr, 100f);
        mgr.Flush();

        root.Update(new UpdateTick(0.16f, 0)); // Pending → Running（快照 LastValue=100）

        mgr.AddAggregatorMod(target, TestAttr, new GameplayEffectHandle(1), 10f, EGameplayModOp.Additive);
        mgr.Flush(); // 110
        root.Update(new UpdateTick(0.16f, 0)); // 第 1 次变化 → Count=1，仍 Running
        Assert.Equal(ETaskState.Running, GetState(task));

        mgr.AddAggregatorMod(target, TestAttr, new GameplayEffectHandle(2), 10f, EGameplayModOp.Additive);
        mgr.Flush(); // 120
        root.Update(new UpdateTick(0.16f, 0)); // 第 2 次变化 → Count=0 → Done

        Assert.Equal(ETaskState.Done, GetState(task));
    }

    [Fact]
    public void TargetDeleted_CompletesTask()
    {
        var (target, task, mgr, root) = Setup(count: 1, condition: EAttributeCondition.Changed);

        mgr.SetAggregatorValue(target, TestAttr, 100f);
        mgr.Flush();

        root.Update(new UpdateTick(0.16f, 0)); // Pending → Running
        target.DeleteEntity();
        root.Update(new UpdateTick(0.16f, 0)); // 目标已销毁 → 无条件 Done

        Assert.Equal(ETaskState.Done, GetState(task));
    }

    // ── 阈值模式（EAttributeCondition.Above / Below）──

    [Fact]
    public void AboveThreshold_CompletesWhenValueExceeds()
    {
        var (target, task, mgr, root) = Setup(condition: EAttributeCondition.Above, threshold: 100f);

        mgr.SetAggregatorValue(target, TestAttr, 50f);
        mgr.Flush();

        root.Update(new UpdateTick(0.16f, 0)); // Pending → Running（50 不 > 100）
        Assert.Equal(ETaskState.Running, GetState(task));

        mgr.AddAggregatorMod(target, TestAttr, new GameplayEffectHandle(1), 60f, EGameplayModOp.Additive);
        mgr.Flush(); // 110 > 100
        root.Update(new UpdateTick(0.16f, 0));

        Assert.Equal(ETaskState.Done, GetState(task));
    }

    [Fact]
    public void AboveThreshold_ValueChangeButStaysBelow_DoesNotComplete()
    {
        var (target, task, mgr, root) = Setup(condition: EAttributeCondition.Above, threshold: 100f);

        mgr.SetAggregatorValue(target, TestAttr, 50f);
        mgr.Flush();

        root.Update(new UpdateTick(0.16f, 0)); // Pending → Running
        Assert.Equal(ETaskState.Running, GetState(task));

        mgr.AddAggregatorMod(target, TestAttr, new GameplayEffectHandle(1), 30f, EGameplayModOp.Additive);
        mgr.Flush(); // 80——值变化了，但仍 < 100 → 不应完成
        root.Update(new UpdateTick(0.16f, 0));

        Assert.Equal(ETaskState.Running, GetState(task));
    }

    [Fact]
    public void BelowThreshold_CompletesWhenValueDrops()
    {
        var (target, task, mgr, root) = Setup(condition: EAttributeCondition.Below, threshold: 50f);

        mgr.SetAggregatorValue(target, TestAttr, 100f);
        mgr.Flush();

        root.Update(new UpdateTick(0.16f, 0)); // Pending → Running（100 不 < 50）
        Assert.Equal(ETaskState.Running, GetState(task));

        mgr.AddAggregatorMod(target, TestAttr, new GameplayEffectHandle(1), -60f, EGameplayModOp.Additive);
        mgr.Flush(); // 40 < 50
        root.Update(new UpdateTick(0.16f, 0));

        Assert.Equal(ETaskState.Done, GetState(task));
    }

    // ── 比值阈值模式（EAttributeCondition.RatioAbove / RatioBelow）──

    [Fact]
    public void RatioAbove_CompletesWhenRatioExceeds()
    {
        var (target, task, mgr, root) = Setup(condition: EAttributeCondition.RatioAbove, threshold: 2f);

        mgr.SetAggregatorValue(target, TestAttr, 30f); // Base=30
        mgr.Flush(); // Current=30, ratio=1.0

        root.Update(new UpdateTick(0.16f, 0)); // Pending → Running（1.0 不 > 2.0）
        Assert.Equal(ETaskState.Running, GetState(task));

        mgr.AddAggregatorMod(target, TestAttr, new GameplayEffectHandle(1), 60f, EGameplayModOp.Additive);
        mgr.Flush(); // Current=90, Base=30, ratio=3.0 > 2.0
        root.Update(new UpdateTick(0.16f, 0));

        Assert.Equal(ETaskState.Done, GetState(task));
    }

    [Fact]
    public void RatioAbove_ValueChangeButRatioBelow_DoesNotComplete()
    {
        var (target, task, mgr, root) = Setup(condition: EAttributeCondition.RatioAbove, threshold: 2f);

        mgr.SetAggregatorValue(target, TestAttr, 30f); // Base=30
        mgr.Flush(); // Current=30, ratio=1.0

        root.Update(new UpdateTick(0.16f, 0)); // Pending → Running
        Assert.Equal(ETaskState.Running, GetState(task));

        mgr.AddAggregatorMod(target, TestAttr, new GameplayEffectHandle(1), 30f, EGameplayModOp.Additive);
        mgr.Flush(); // Current=60, Base=30, ratio=2.0——值变了，但 2.0 不 > 2.0 → 不应完成
        root.Update(new UpdateTick(0.16f, 0));

        Assert.Equal(ETaskState.Running, GetState(task));
    }

    [Fact]
    public void RatioAbove_BaseZero_DoesNotComplete()
    {
        var (target, task, mgr, root) = Setup(condition: EAttributeCondition.RatioAbove, threshold: 2f);

        mgr.SetAggregatorValue(target, TestAttr, 0f); // Base=0
        mgr.Flush();

        root.Update(new UpdateTick(0.16f, 0)); // Pending → Running
        Assert.Equal(ETaskState.Running, GetState(task));

        // Base=0 时比值无意义——0/0=NaN、x/0=Infinity 都会误判，必须跳过判定
        mgr.AddAggregatorMod(target, TestAttr, new GameplayEffectHandle(1), 50f, EGameplayModOp.Additive);
        mgr.Flush(); // Current=50, Base=0
        root.Update(new UpdateTick(0.16f, 0));

        Assert.Equal(ETaskState.Running, GetState(task));
    }
}
