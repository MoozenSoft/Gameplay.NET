// src/Gameplay/Gameplay.Abilities/AbilityTask/WaitCancelTask.cs
using Friflo.Engine.ECS;
using Gameplay.Tasks;

namespace Gameplay.Abilities;

/// <summary>
/// WaitCancel Task 标记 Component —— 仅作为标记，不存储额外数据。<br/>
/// 当 ActiveAbility 被取消时，AbilityActivationSystem.CancelAbility 会遍历子 Entity，
/// 将所有挂有该标记的 Task Entity 的 TaskState 设为 Done。
/// </summary>
public struct WaitCancelComponent : IComponent
{
}
