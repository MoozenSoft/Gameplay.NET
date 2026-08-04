using Friflo.Engine.ECS;

namespace Gameplay.Tasks;

/// <summary>延时能力——累积 Elapsed 到达 Duration 后 Task 完成（Done）。</summary>
public struct DelayComponent : IComponent
{
    public float Duration;
    public float Elapsed;
}
