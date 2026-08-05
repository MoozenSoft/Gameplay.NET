// tests/Gameplay.Tests/Gameplay.Tests.Tasks/MoveToSystemTests.cs
namespace Gameplay.Tests.Tasks;

using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Gameplay.Tasks;
using Xunit;

/// <summary>对应 src/Gameplay/Gameplay.Tasks/Systems/MoveToSystem.cs——Duration 插值模型（对齐 UE5 AbilityTask_MoveToLocation）。</summary>
public class MoveToSystemTests
{
    private static (Entity Target, Entity Task, SystemRoot Root) Setup(Position start, Position destination, float duration)
    {
        var store = new EntityStore();
        var target = store.CreateEntity();
        target.AddComponent(start);

        var task = store.CreateEntity();
        task.AddComponent(new TaskStateComponent { State = ETaskState.Pending });
        task.AddComponent(new MoveToComponent { Target = target, Destination = destination, Duration = duration });

        var root = new SystemRoot(store) { new MoveToSystem() };
        return (target, task, root);
    }

    private static Position GetPosition(Entity target)
        => target.GetComponent<Position>();

    private static ETaskState GetState(Entity task)
        => task.GetComponent<TaskStateComponent>().State;

    [Fact]
    public void InterpolatesTowardDestination_ByDurationProgress()
    {
        // Duration=2s，0→10：第一帧 alpha = 0.16/2 = 0.08 → x = 0.8
        var (target, task, root) = Setup(new Position(0f, 0f, 0f), new Position(10f, 0f, 0f), duration: 2f);

        root.Update(new UpdateTick(0.16f, 0)); // Pending → Running（快照 Start）+ 插值

        Assert.Equal(ETaskState.Running, GetState(task));
        var pos = GetPosition(target);
        Assert.Equal(0.8f, pos.x, 4);
        Assert.Equal(0f, pos.y, 4);
    }

    [Fact]
    public void CompletesWhenDurationElapses_AtExactDestination()
    {
        // Duration=0.32s（2 帧）：帧1 alpha=0.5，帧2 alpha=1 → 完成
        var (target, task, root) = Setup(new Position(0f, 0f, 0f), new Position(2f, 0f, 0f), duration: 0.32f);

        root.Update(new UpdateTick(0.16f, 0));
        root.Update(new UpdateTick(0.16f, 0));
        root.Update(new UpdateTick(0.16f, 0)); // 超出时长 → clamp alpha=1

        Assert.Equal(ETaskState.Done, GetState(task));
        var pos = GetPosition(target);
        Assert.Equal(2f, pos.x, 4); // 精确停在目标位置（不超调）
    }

    [Fact]
    public void AlreadyAtDestination_CompletesWhenDurationElapses()
    {
        // Duration 模型：即使已在目标，也等时长走完（UE5 语义——插值无视觉变化但时长照走）
        var (target, task, root) = Setup(new Position(3f, 3f, 3f), new Position(3f, 3f, 3f), duration: 0.32f);

        root.Update(new UpdateTick(0.16f, 0));
        Assert.Equal(ETaskState.Running, GetState(task));

        root.Update(new UpdateTick(0.16f, 0)); // 时长结束 → Done
        Assert.Equal(ETaskState.Done, GetState(task));
    }

    [Fact]
    public void ZeroDuration_CompletesImmediately()
    {
        var (target, task, root) = Setup(new Position(0f, 0f, 0f), new Position(5f, 0f, 0f), duration: 0f);

        root.Update(new UpdateTick(0.16f, 0)); // Duration<=0 → 立即完成

        Assert.Equal(ETaskState.Done, GetState(task));
        Assert.Equal(5f, GetPosition(target).x, 4); // 直接落在目标
    }

    [Fact]
    public void TargetWithoutPosition_Completes()
    {
        var store = new EntityStore();
        var target = store.CreateEntity(); // 无 Position
        var task = store.CreateEntity();
        task.AddComponent(new TaskStateComponent { State = ETaskState.Pending });
        task.AddComponent(new MoveToComponent { Target = target, Destination = new Position(0f, 0f, 0f), Duration = 1f });

        var root = new SystemRoot(store) { new MoveToSystem() };

        root.Update(new UpdateTick(0.16f, 0)); // Pending：无法快照 Start → 防御性完成
        root.Update(new UpdateTick(0.16f, 0));

        Assert.Equal(ETaskState.Done, GetState(task));
    }

    [Fact]
    public void TargetDeleted_Completes()
    {
        var (target, task, root) = Setup(new Position(0f, 0f, 0f), new Position(10f, 0f, 0f), duration: 2f);

        root.Update(new UpdateTick(0.16f, 0)); // Pending → Running
        target.DeleteEntity();
        root.Update(new UpdateTick(0.16f, 0)); // 目标已销毁 → 防御性完成

        Assert.Equal(ETaskState.Done, GetState(task));
    }
}
