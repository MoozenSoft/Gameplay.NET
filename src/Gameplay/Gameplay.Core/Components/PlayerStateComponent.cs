using Friflo.Engine.ECS;

namespace Gameplay.Core;

/// <summary>玩家身份（名字经 PlayerId 查外部表，Component 不存 string）。</summary>
public struct PlayerStateComponent : IComponent
{
    public int PlayerId;
}
