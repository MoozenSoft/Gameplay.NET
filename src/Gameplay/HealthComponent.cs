using Friflo.Engine.ECS;

namespace Gameplay;

/// <summary>
/// 生命值组件。纯数据，行为由 System 定义。
/// </summary>
public struct HealthComponent : IComponent
{
    /// <summary>当前生命值。</summary>
    public float Value;
}
