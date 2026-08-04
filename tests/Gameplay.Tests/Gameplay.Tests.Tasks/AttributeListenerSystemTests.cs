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

    private static (Entity Target, Entity Task, AttributeAggregatorManager Mgr, SystemRoot Root) Setup(int count)
    {
        var store = new EntityStore();
        var mgr = new AttributeAggregatorManager();
        var target = store.CreateEntity();

        var task = store.CreateEntity();
        task.AddComponent(new TaskStateComponent { State = ETaskState.Pending });
        task.AddComponent(new AttributeListener { Target = target, Attribute = TestAttr, Count = count });

        var root = new SystemRoot(store) { new AttributeListenerSystem(mgr) };
        return (target, task, mgr, root);
    }

    private static ETaskState GetState(Entity task)
        => task.GetComponent<TaskStateComponent>().State;

    [Fact]
    public void ValueChange_CompletesTask()
    {
        var (target, task, mgr, root) = Setup(count: 1);

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
        var (target, task, mgr, root) = Setup(count: 1);

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
        var (target, task, mgr, root) = Setup(count: 2);

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
        var (target, task, mgr, root) = Setup(count: 1);

        mgr.SetAggregatorValue(target, TestAttr, 100f);
        mgr.Flush();

        root.Update(new UpdateTick(0.16f, 0)); // Pending → Running
        target.DeleteEntity();
        root.Update(new UpdateTick(0.16f, 0)); // 目标已销毁 → 无条件 Done

        Assert.Equal(ETaskState.Done, GetState(task));
    }
}
