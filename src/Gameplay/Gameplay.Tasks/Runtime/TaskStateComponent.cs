using Friflo.Engine.ECS;

namespace Gameplay.Tasks;

public enum ETaskState
{
    Pending,
    Running,
    Done,
    Cancelled,
}

/// <summary>Task 的运行状态。</summary>
public struct TaskStateComponent : IComponent
{
    public ETaskState State;
}
