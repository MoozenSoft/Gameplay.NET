using Friflo.Engine.ECS;

namespace Gameplay.Abilities;

/// <summary>GameplayCue 执行时传递的参数。</summary>
public struct GameplayCueParameters
{
    /// <summary>Cue 的触发者（来源 Entity）。</summary>
    public Entity Instigator;

    /// <summary>归一化幅度（0.0 ~ 1.0），表示效果强度。</summary>
    public float NormalizedMagnitude;
}
