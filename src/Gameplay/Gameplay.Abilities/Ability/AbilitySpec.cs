using Gameplay.Tags;

namespace Gameplay.Abilities;

/// <summary>Ability 授予实例数据（非 Entity），存在 AbilityCollectionComponent 中。</summary>
public struct AbilitySpec
{
    public int Handle;                                      // 本 Spec 的唯一标识
    public GameplayAbility Ability;                         // 静态定义引用
    public int Level;                                       // 能力等级
    public int InputID;                                     // 输入绑定（-1 = 未绑定）
    public EGrantedAbilityRemovePolicy RemovalPolicy;       // 移除策略
    public GameplayTagContainer DynamicSpecSourceTags;      // 运行时附加 Tag
}
