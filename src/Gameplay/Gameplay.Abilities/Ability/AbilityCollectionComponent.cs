using Friflo.Engine.ECS;

namespace Gameplay.Abilities;

/// <summary>角色拥有的 Ability 集合。挂在 Owner Entity 上。</summary>
public struct AbilityCollectionComponent : IComponent
{
    public AbilitySpec[] Specs;   // 预分配或动态扩展
}
