using Gameplay.Tags;

namespace Gameplay.Abilities;

/// <summary>条件触发的 GameplayEffect——用于 OnApplicationEffects 和 OnCompleteEffects。</summary>
public struct ConditionalGameplayEffect
{
    public GameplayEffect Effect;
    public GameplayTagContainer RequiredSourceTags; // 为空 = 无条件触发
}
