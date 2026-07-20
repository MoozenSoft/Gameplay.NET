using Friflo.Engine.ECS;

namespace Gameplay;

/// <summary>Task 拥有者引用。v1 为数据占位，留待 AbilityInstance 使用。</summary>
public struct TaskOwnerComponent : IComponent
{
    public Entity Owner;
}
