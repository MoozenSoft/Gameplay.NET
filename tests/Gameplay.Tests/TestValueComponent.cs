using Friflo.Engine.ECS;

namespace Gameplay.Tests;

/// <summary>
/// 测试专用占位组件。仅用于验证 ECS 查询、克隆等框架机制，无业务语义。
/// </summary>
public struct TestValueComponent : IComponent
{
    /// <summary>测试用数值。</summary>
    public float Value;
}
