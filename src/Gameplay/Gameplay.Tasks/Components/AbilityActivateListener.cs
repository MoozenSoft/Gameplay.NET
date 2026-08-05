using Friflo.Engine.ECS;
using Gameplay.Tags;

namespace Gameplay.Tasks;

/// <summary>Ability 激活监听能力——事件驱动：AbilityActivationManager 成功激活 Ability 时，
/// 匹配（Character + AssetTags）的 Task 完成（Done）。</summary>
/// <remarks>
/// <para>对应 UE 的 WaitAbilityActivate。</para>
/// <para><see cref="Character"/> 为无符号 = 匹配任何角色的激活；<see cref="AbilityTags"/> 为 null/空 = 匹配任何 Ability。</para>
/// <para><see cref="AbilityTags"/> 是可变 class 引用——调用者不得在 Task 存活期间修改它。</para>
/// </remarks>
public struct AbilityActivateListener : IComponent
{
    /// <summary>监听谁激活了 Ability（无符号 = 任何角色）。</summary>
    public Entity Character;

    /// <summary>匹配激活 Ability 的 AssetTags（null/空 = 匹配任何）。</summary>
    public GameplayTagContainer? AbilityTags;
}
