// tests/Gameplay.Tests/Gameplay.Tests.Tasks/TagListenerSystemTests.cs
namespace Gameplay.Tests.Tasks;

using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Gameplay.Tasks;
using Gameplay.Tags;
using Xunit;

/// <summary>对应 src/Gameplay/Gameplay.Tasks/Systems/TagListenerSystem.cs。</summary>
public class TagListenerSystemTests
{
    private static readonly GameplayTag TestTag = CreateTestTag();

    private static GameplayTag CreateTestTag()
    {
        GameplayTagManager.RegisterTags("Test.Tag"); // Request 是只读查找，必须先注册
        return GameplayTag.Request("Test.Tag");
    }

    private static (Entity Target, Entity Task, SystemRoot Root) Setup(TagCondition condition)
        => SetupListener(condition, TestTag, null);

    private static ETaskState GetState(Entity task)
        => task.GetComponent<TaskStateComponent>().State;

    [Fact]
    public void Added_Condition_CompletesWhenTagAppears()
    {
        var (target, task, root) = Setup(TagCondition.Added);

        root.Update(new UpdateTick(0.16f, 0)); // Pending → Running，Tag 未出现
        Assert.Equal(ETaskState.Running, GetState(task));

        target.GetComponent<GameplayTagsComponent>().AddTag(TestTag);
        root.Update(new UpdateTick(0.16f, 0)); // Tag 出现 → Done

        Assert.Equal(ETaskState.Done, GetState(task));
    }

    [Fact]
    public void Added_Condition_StaysRunningWhenTagAbsent()
    {
        var (target, task, root) = Setup(TagCondition.Added);

        root.Update(new UpdateTick(0.16f, 0));
        root.Update(new UpdateTick(0.16f, 0));

        Assert.Equal(ETaskState.Running, GetState(task));
    }

    [Fact]
    public void Removed_Condition_CompletesWhenTagRemoved()
    {
        var (target, task, root) = Setup(TagCondition.Removed);
        target.GetComponent<GameplayTagsComponent>().AddTag(TestTag);

        root.Update(new UpdateTick(0.16f, 0)); // Pending：Tag 存在 → WasPresent=true → Running
        Assert.Equal(ETaskState.Running, GetState(task));
        ref var listener = ref task.GetComponent<TagListenerComponent>();
        Assert.True(listener.WasPresent);

        target.GetComponent<GameplayTagsComponent>().RemoveTag(TestTag);
        root.Update(new UpdateTick(0.16f, 0)); // Tag 被移除 → Done

        Assert.Equal(ETaskState.Done, GetState(task));
    }

    [Fact]
    public void Removed_Condition_CompletesImmediatelyWhenTagAbsentAtRegistration()
    {
        var (target, task, root) = Setup(TagCondition.Removed);

        // 注册时 Tag 不存在 → Removed 条件已满足 → Pending 阶段直接 Done
        root.Update(new UpdateTick(0.16f, 0));

        Assert.Equal(ETaskState.Done, GetState(task));
    }

    [Fact]
    public void Removed_Condition_StaysRunningWhileTagPresent()
    {
        var (target, task, root) = Setup(TagCondition.Removed);
        target.GetComponent<GameplayTagsComponent>().AddTag(TestTag);

        root.Update(new UpdateTick(0.16f, 0)); // WasPresent=true → Running
        root.Update(new UpdateTick(0.16f, 0)); // Tag 仍在 → 不 Done

        Assert.Equal(ETaskState.Running, GetState(task));
    }

    // ── Query 模式（RequiredTags：全部满足才视为"出现"）──

    private static readonly GameplayTag TestTag2 = CreateTestTag2();

    private static GameplayTag CreateTestTag2()
    {
        GameplayTagManager.RegisterTags("Test.Tag2");
        return GameplayTag.Request("Test.Tag2");
    }

    private static (Entity Target, Entity Task, SystemRoot Root) SetupQuery(TagCondition condition)
        => SetupListener(condition, default, new GameplayTagContainer { TestTag, TestTag2 });

    /// <summary>共享测试环境：目标（带 GameplayTagsComponent）+ Task（TaskState + TagListenerComponent 指定模式）+ SystemRoot。</summary>
    private static (Entity Target, Entity Task, SystemRoot Root) SetupListener(
        TagCondition condition, GameplayTag tag, GameplayTagContainer? required)
    {
        var store = new EntityStore();
        var target = store.CreateEntity();
        target.AddComponent(new GameplayTagsComponent());

        var task = store.CreateEntity();
        task.AddComponent(new TaskStateComponent { State = ETaskState.Pending });
        task.AddComponent(new TagListenerComponent
        {
            Target = target,
            Tag = tag,
            RequiredTags = required,
            Condition = condition,
        });

        var root = new SystemRoot(store) { new TagListenerSystem() };
        return (target, task, root);
    }

    [Fact]
    public void QueryAdded_CompletesWhenAllTagsPresent()
    {
        var (target, task, root) = SetupQuery(TagCondition.Added);

        target.GetComponent<GameplayTagsComponent>().AddTag(TestTag); // 部分满足
        root.Update(new UpdateTick(0.16f, 0)); // Pending → Running
        Assert.Equal(ETaskState.Running, GetState(task));

        target.GetComponent<GameplayTagsComponent>().AddTag(TestTag2); // 全部满足
        root.Update(new UpdateTick(0.16f, 0));

        Assert.Equal(ETaskState.Done, GetState(task));
    }

    [Fact]
    public void QueryAdded_StaysRunningWhenPartial()
    {
        var (target, task, root) = SetupQuery(TagCondition.Added);

        target.GetComponent<GameplayTagsComponent>().AddTag(TestTag);
        root.Update(new UpdateTick(0.16f, 0));
        root.Update(new UpdateTick(0.16f, 0));

        Assert.Equal(ETaskState.Running, GetState(task));
    }

    [Fact]
    public void QueryRemoved_CompletesWhenConditionBroken()
    {
        var (target, task, root) = SetupQuery(TagCondition.Removed);
        target.GetComponent<GameplayTagsComponent>().AddTag(TestTag);
        target.GetComponent<GameplayTagsComponent>().AddTag(TestTag2);

        root.Update(new UpdateTick(0.16f, 0)); // Pending：全部存在 → WasPresent=true → Running
        Assert.Equal(ETaskState.Running, GetState(task));

        // AND 语义：移除任一 Tag 即破坏"全部出现"条件 → Done
        target.GetComponent<GameplayTagsComponent>().RemoveTag(TestTag);
        root.Update(new UpdateTick(0.16f, 0));

        Assert.Equal(ETaskState.Done, GetState(task));
    }

    [Fact]
    public void QueryRemoved_CompletesImmediatelyWhenMissingAtRegistration()
    {
        var (target, task, root) = SetupQuery(TagCondition.Removed);
        target.GetComponent<GameplayTagsComponent>().AddTag(TestTag); // 只注册了一个，不全

        root.Update(new UpdateTick(0.16f, 0)); // Pending：并非全部存在 → 条件已满足 → 立即 Done

        Assert.Equal(ETaskState.Done, GetState(task));
    }

    [Fact]
    public void TargetWithoutTagsComponent_Completes()
    {
        var store = new EntityStore();
        var target = store.CreateEntity(); // 无 GameplayTagsComponent
        var task = store.CreateEntity();
        task.AddComponent(new TaskStateComponent { State = ETaskState.Pending });
        task.AddComponent(new TagListenerComponent { Target = target, Tag = TestTag, Condition = TagCondition.Added });

        var root = new SystemRoot(store) { new TagListenerSystem() };

        root.Update(new UpdateTick(0.16f, 0)); // Pending → Running
        root.Update(new UpdateTick(0.16f, 0)); // 目标无 Tags 组件 → Done

        Assert.Equal(ETaskState.Done, GetState(task));
    }
}
