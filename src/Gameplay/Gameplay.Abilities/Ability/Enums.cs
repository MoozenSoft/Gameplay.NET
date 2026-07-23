// src/Gameplay/Gameplay.Abilities/Ability/Enums.cs
using System;

namespace Gameplay.Abilities;

/// <summary>Ability 网络执行策略。</summary>
public enum EGameplayAbilityNetExecutionPolicy
{
    LocalPredicted,    // Client 立即执行 → Server 确认/回滚
    LocalOnly,         // 只在本地执行
    ServerInitiated,   // Server 发起，Client 本地也执行
    ServerOnly,        // 只在 Server 执行
}

/// <summary>Ability 网络安全策略。</summary>
public enum EGameplayAbilityNetSecurityPolicy
{
    ClientOrServer,           // 无限制
    ServerOnlyExecution,      // 只有 Server 能发起
    ServerOnlyTermination,    // 只有 Server 能终止
    ServerOnly,               // 只有 Server 能发起和终止
}

/// <summary>Ability 触发来源。</summary>
public enum EAbilityTriggerSource
{
    GameplayEvent,     // GameplayEvent 触发
    OwnedTagAdded,     // Owner 获得指定 Tag 时触发
    OwnedTagPresent,   // Owner 拥有指定 Tag 时持续激活
}

/// <summary>Ability 授予移除策略。</summary>
public enum EGrantedAbilityRemovePolicy
{
    CancelAbilityImmediately,  // 立即取消并移除
    RemoveAbilityOnEnd,        // 能力结束后移除
    DoNothing,                 // 不移除
}

/// <summary>Ability 激活请求来源。</summary>
public enum ActivationSource
{
    Input,
    AI,
    GameplayEvent,
    Network,
    TagTrigger,
}

/// <summary>ActiveAbility 运行状态。</summary>
public enum AbilityInstanceState
{
    Activating,    // 正在激活（Requirements 通过，Commit 执行中）
    Active,        // 激活中
    Ending,        // 正在结束（清理 Tags/Tasks）
    Cancelled,     // 已取消
    Completed,     // 已完成
}
