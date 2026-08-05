using Friflo.Engine.ECS;

namespace Gameplay.Tasks;

/// <summary>输入触发条件。</summary>
public enum EInputTrigger
{
    /// <summary>本帧按下（上升沿）。</summary>
    Press,
    /// <summary>本帧释放（下降沿）。</summary>
    Release,
    /// <summary>当前按住。</summary>
    Hold,
}

/// <summary>输入监听能力——每帧轮询 <see cref="Gameplay.Interfaces.IInputService"/>，满足触发条件时 Task 完成（Done）。</summary>
/// <remarks>
/// <para>对应 UE 的 WaitInputPress / WaitInputRelease。同一 System 内按 <see cref="Trigger"/> 分支——同一能力内的条件分支。</para>
/// </remarks>
public struct InputListener : IComponent
{
    /// <summary>输入动作 ID（策划配置的动作标识）。</summary>
    public int ActionId;

    /// <summary>触发条件（Press / Release / Hold）。</summary>
    public EInputTrigger Trigger;
}
