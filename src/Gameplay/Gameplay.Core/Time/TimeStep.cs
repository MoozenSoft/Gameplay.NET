namespace Gameplay.Core;

/// <summary>模拟步长模式。</summary>
public enum ETimeStep
{
    /// <summary>可变步长（每帧一次，dt 随渲染帧）。</summary>
    Variable,

    /// <summary>固定步长（累积器 + 可能多子步）。v1 仅占位，完整实现后置。</summary>
    Fixed,
}
