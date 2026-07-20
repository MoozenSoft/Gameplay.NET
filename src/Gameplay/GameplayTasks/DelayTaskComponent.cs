using Friflo.Engine.ECS;

namespace Gameplay.GameplayTasks;

/// <summary>延时等待——累积 Elapsed 到达 Duration 后 Done。</summary>
public struct DelayTaskComponent : IComponent
{
    public float Duration;
    public float Elapsed;
}
