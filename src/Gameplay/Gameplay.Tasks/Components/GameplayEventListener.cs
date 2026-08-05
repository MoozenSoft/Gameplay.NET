using Friflo.Engine.ECS;

namespace Gameplay.Tasks;

/// <summary>事件监听能力——监听指定的 GameplayEvent。</summary>
/// <remarks>
/// <para>当 GameplayEventDispatcher 分发匹配的 GameplayEvent 时，Task 完成（Done）。</para>
/// </remarks>
public struct GameplayEventListener : IComponent
{
    /// <summary>要监听的事件 ID。</summary>
    public ushort EventId;
}
