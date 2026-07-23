using Gameplay.Tags;

namespace Gameplay.Abilities;

/// <summary>GameplayEffect 关联的 Cue 定义。</summary>
public struct GameplayEffectCue
{
    public GameplayTag CueTag;
    public float MinLevel;
    public float MaxLevel;
}
