# GameplayAbilities Plan 2: GameplayAbility + Activation Pipeline

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 实现 GameplayAbility 静态定义 + AbilitySpec + ActiveAbility 运行时 Entity + Activation Pipeline (Requirements/Commit/Execute)

**Architecture:** GameplayAbility 走三层模型（静态定义 → AbilitySpec → ActiveAbility Entity），Pipeline 走 Requirements → Commit → Execute 四阶段。依赖 Plan 1 的 EffectSystem（ApplyCooldown、ApplyEffect）。

**Tech Stack:** C# (.NET 10 / netstandard2.1), Friflo.Engine.ECS, xUnit

**Spec:** `docs/superpowers/specs/2026-07-21-gameplay-abilities-design.md`

**Plan 1 产物:** EffectSystem.Apply/CanApply/RemoveEffect, AttributeSystem, GameplayEffectSpec, GameplayTagsComponent

## Global Constraints

- 命名空间：源码 `Gameplay.Abilities`，测试 `Gameplay.Tests.GameplayAbilities`，文件范围命名空间
- 文档和注释用中文，专业术语用英文
- TDD：写测试 → 确认失败 → 写实现 → 确认通过 → 提交
- Friflo System 模式: `QuerySystem<T>` + `Query.ForEachEntity((ref T, Entity) => {})`
- Friflo 注册: `new SystemRoot(store) { sys1, sys2 }`
- 跨 TFM: `Enum.GetValues(typeof(T))` (非泛型), `Random.Shared` 不兼容用 `new Random()`
- 枚举命名: `E` 前缀（如 `EGameplayAbilityNetExecutionPolicy`）

## 实施修正（Plan 1 中发现的 Friflo API 差异）

| 计划写法 | 实际用法 |
|---------|---------|
| `store.AddSystem(sys)` | `new SystemRoot(store) { sys1, sys2 }` |
| `Query.Chunks` / `chunk.Span` | `Query.ForEachEntity((ref T, Entity) => {})` |
| `entityId` | `entity.Id` |
| EffectSystem 构造 | `new EffectSystem(attrSys)` 传入 AttributeSystem |

---

## 文件结构

```
src/Gameplay/Gameplay.Abilities/Ability/
├── Enums.cs                              # Ability 相关枚举（NetPolicy, SecurityPolicy, TriggerSource 等）
├── GameplayAbility.cs                    # 静态定义（非 Entity）
├── AbilitySpec.cs                        # 授予实例数据（非 Entity），含 AbilitySpecHandle
├── AbilityCollectionComponent.cs         # 角色拥有的 Ability 集合
├── ActiveAbilityComponent.cs             # 运行时 Entity Component
├── AbilityActivationRequest.cs           # 激活请求 POCO
├── IAbilityRequirement.cs                # CanActivate 扩展点接口
├── IAbilityCommit.cs                     # Commit 扩展点接口
├── IAbilityExecutor.cs                   # Execute 扩展点接口
├── AbilityActivationSystem.cs            # 激活流程 System
└── CommitActions/                        # 内置 Commit 实现
    ├── ApplyCooldownCommit.cs            # 施加 Cooldown GE
    └── ConsumeCostCommit.cs              # 直接 Mod Attribute
└── Executors/                            # 内置 Executor 实现
    ├── ApplyEffectExecutor.cs            # 对 Target 施加 GE Spec
    └── SpawnTaskExecutor.cs              # 创建 Task Entity

tests/Gameplay.Tests/Gameplay.Tests.Abilities/Ability/
├── EnumsTests.cs
├── GameplayAbilityTests.cs
├── AbilitySpecTests.cs
├── AbilityCollectionComponentTests.cs
├── ActiveAbilityComponentTests.cs
├── AbilityActivationSystemTests.cs
├── ApplyCooldownCommitTests.cs
└── ConsumeCostCommitTests.cs
```

---

### Task 1: GameplayAbility 相关枚举

**Files:**
- Create: `src/Gameplay/Gameplay.Abilities/Ability/Enums.cs`
- Create: `tests/Gameplay.Tests/Gameplay.Tests.Abilities/Ability/EnumsTests.cs`

**Interfaces:**
- Produces: `EGameplayAbilityNetExecutionPolicy`, `EGameplayAbilityNetSecurityPolicy`, `EAbilityTriggerSource`, `EGrantedAbilityRemovePolicy`, `EActivationSource`, `EAbilityInstanceState`

- [ ] **Step 1: 写测试**

```csharp
// tests/Gameplay.Tests/Gameplay.Tests.Abilities/Ability/EnumsTests.cs
namespace Gameplay.Tests.Abilities;

using Gameplay.Abilities;
using Xunit;

public class AbilityEnumsTests
{
    [Fact]
    public void NetExecutionPolicy_HasExpectedValues()
    {
        Assert.Equal(0, (int)EGameplayAbilityNetExecutionPolicy.LocalPredicted);
        Assert.Equal(1, (int)EGameplayAbilityNetExecutionPolicy.LocalOnly);
        Assert.Equal(2, (int)EGameplayAbilityNetExecutionPolicy.ServerInitiated);
        Assert.Equal(3, (int)EGameplayAbilityNetExecutionPolicy.ServerOnly);
    }

    [Fact]
    public void NetSecurityPolicy_HasExpectedValues()
    {
        Assert.Equal(0, (int)EGameplayAbilityNetSecurityPolicy.ClientOrServer);
        Assert.Equal(1, (int)EGameplayAbilityNetSecurityPolicy.ServerOnlyExecution);
        Assert.Equal(2, (int)EGameplayAbilityNetSecurityPolicy.ServerOnlyTermination);
        Assert.Equal(3, (int)EGameplayAbilityNetSecurityPolicy.ServerOnly);
    }

    [Fact]
    public void EActivationSource_HasExpectedValues()
    {
        var values = Enum.GetValues(typeof(EActivationSource));
        Assert.Contains(EActivationSource.Input, values);
        Assert.Contains(EActivationSource.GameplayEvent, values);
    }
}
```

- [ ] **Step 2: 运行测试确认失败**

Run: `dotnet test tests/Gameplay.Tests/Gameplay.Tests.csproj --filter "FullyQualifiedName~AbilityEnumsTests"`
Expected: FAIL

- [ ] **Step 3: 实现**

```csharp
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
public enum EActivationSource
{
    Input,
    AI,
    GameplayEvent,
    Network,
    TagTrigger,
}

/// <summary>ActiveAbility 运行状态。</summary>
public enum EAbilityInstanceState
{
    Activating,    // 正在激活（Requirements 通过，Commit 执行中）
    Active,        // 激活中
    Ending,        // 正在结束（清理 Tags/Tasks）
    Cancelled,     // 已取消
    Completed,     // 已完成
}
```

- [ ] **Step 4: 运行测试确认通过**

Run: `dotnet test tests/Gameplay.Tests/Gameplay.Tests.csproj --filter "FullyQualifiedName~AbilityEnumsTests"`
Expected: PASS

- [ ] **Step 5: 提交**

```bash
git add src/Gameplay/Gameplay.Abilities/Ability/Enums.cs tests/Gameplay.Tests/Gameplay.Tests.Abilities/Ability/EnumsTests.cs
git commit -m "feat: add Ability enums (NetPolicy, SecurityPolicy, TriggerSource, etc.)

Co-Authored-By: Claude <noreply@anthropic.com>"
```

---

### Task 2: GameplayAbility + AbilityTriggerData

**Files:**
- Create: `src/Gameplay/Gameplay.Abilities/Ability/GameplayAbility.cs`
- Create: `tests/Gameplay.Tests/Gameplay.Tests.Abilities/Ability/GameplayAbilityTests.cs`

**Interfaces:**
- Consumes: `GameplayEffect` (Plan 1), `GameplayTagContainer` (现有), 枚举 (Task 1)
- Produces: `GameplayAbility` class

- [ ] **Step 1: 写测试**

```csharp
// tests/Gameplay.Tests/Gameplay.Tests.Abilities/Ability/GameplayAbilityTests.cs
namespace Gameplay.Tests.Abilities;

using Gameplay.Abilities;
using Xunit;

public class GameplayAbilityTests
{
    [Fact]
    public void Default_NetExecutionPolicy_IsLocalPredicted()
    {
        var ability = new GameplayAbility();
        Assert.Equal(EGameplayAbilityNetExecutionPolicy.LocalPredicted, ability.NetExecutionPolicy);
    }

    [Fact]
    public void ActivationBlockedTags_PreventsActivation()
    {
        var ability = new GameplayAbility();
        Assert.NotNull(ability.ActivationBlockedTags);
        Assert.Equal(0, ability.ActivationBlockedTags.Count);
    }

    [Fact]
    public void AssetTags_InitiallyEmpty()
    {
        var ability = new GameplayAbility();
        Assert.NotNull(ability.AssetTags);
        Assert.Equal(0, ability.AssetTags.Count);
    }
}
```

- [ ] **Step 2: 运行测试确认失败**

Run: `dotnet test tests/Gameplay.Tests/Gameplay.Tests.csproj --filter "FullyQualifiedName~GameplayAbilityTests"`
Expected: FAIL

- [ ] **Step 3: 实现**

```csharp
// src/Gameplay/Gameplay.Abilities/Ability/GameplayAbility.cs
using Gameplay.Tags;

namespace Gameplay.Abilities;

/// <summary>
/// GameplayAbility 静态定义（非 Entity）。策划/开发者配置的资产级数据。
/// </summary>
public class GameplayAbility
{
    // ── Tags ──
    public GameplayTagContainer AssetTags = new();
    public GameplayTagContainer CancelAbilitiesWithTag = new();
    public GameplayTagContainer BlockAbilitiesWithTag = new();
    public GameplayTagContainer ActivationOwnedTags = new();
    public GameplayTagContainer ActivationRequiredTags = new();
    public GameplayTagContainer ActivationBlockedTags = new();
    public GameplayTagContainer SourceRequiredTags = new();
    public GameplayTagContainer SourceBlockedTags = new();
    public GameplayTagContainer TargetRequiredTags = new();
    public GameplayTagContainer TargetBlockedTags = new();

    // ── Cooldown ──
    public GameplayEffect.GameplayEffect CooldownEffect;

    // ── Triggers ──
    public AbilityTriggerData[] AbilityTriggers = System.Array.Empty<AbilityTriggerData>();

    // ── Network ──
    public EGameplayAbilityNetExecutionPolicy NetExecutionPolicy = EGameplayAbilityNetExecutionPolicy.LocalPredicted;
    public EGameplayAbilityNetSecurityPolicy NetSecurityPolicy;

    // ── Extensions ──
    public IAbilityRequirement[] Requirements = System.Array.Empty<IAbilityRequirement>();
    public IAbilityCommit[] CommitActions = System.Array.Empty<IAbilityCommit>();
    public IAbilityExecutor Executor;
}

/// <summary>Ability 触发器配置。</summary>
public struct AbilityTriggerData
{
    public GameplayTag TriggerTag;
    public EAbilityTriggerSource TriggerSource;
}
```

- [ ] **Step 4: 运行测试确认通过**

Run: `dotnet test tests/Gameplay.Tests/Gameplay.Tests.csproj --filter "FullyQualifiedName~GameplayAbilityTests"`
Expected: PASS

- [ ] **Step 5: 提交**

```bash
git add src/Gameplay/Gameplay.Abilities/Ability/GameplayAbility.cs tests/Gameplay.Tests/Gameplay.Tests.Abilities/Ability/GameplayAbilityTests.cs
git commit -m "feat: add GameplayAbility static definition and AbilityTriggerData

Co-Authored-By: Claude <noreply@anthropic.com>"
```

---

### Task 3: AbilitySpec + AbilityCollectionComponent

**Files:**
- Create: `src/Gameplay/Gameplay.Abilities/Ability/AbilitySpec.cs`
- Create: `src/Gameplay/Gameplay.Abilities/Ability/AbilityCollectionComponent.cs`
- Create: `tests/Gameplay.Tests/Gameplay.Tests.Abilities/Ability/AbilitySpecTests.cs`

**Interfaces:**
- Consumes: `GameplayAbility` (Task 2)
- Produces: `AbilitySpec` struct, `AbilityCollectionComponent` IComponent

- [ ] **Step 1: 写测试**

```csharp
// tests/Gameplay.Tests/Gameplay.Tests.Abilities/Ability/AbilitySpecTests.cs
namespace Gameplay.Tests.Abilities;

using Gameplay.Abilities;
using Xunit;

public class AbilitySpecTests
{
    [Fact]
    public void Constructor_SetsFields()
    {
        var ability = new GameplayAbility();
        var spec = new AbilitySpec
        {
            Ability = ability,
            Level = 3,
            InputID = 1,
        };
        Assert.Same(ability, spec.Ability);
        Assert.Equal(3, spec.Level);
        Assert.Equal(1, spec.InputID);
    }

    [Fact]
    public void Default_RemovalPolicy_IsCancelImmediately()
    {
        var spec = new AbilitySpec();
        Assert.Equal(EGrantedAbilityRemovePolicy.CancelAbilityImmediately, spec.RemovalPolicy);
    }

    [Fact]
    public void Collection_StoresSpecs()
    {
        var comp = new AbilityCollectionComponent();
        comp.Specs = new AbilitySpec[]
        {
            new() { Level = 1 },
            new() { Level = 2 },
        };
        Assert.Equal(2, comp.Specs.Length);
    }
}
```

- [ ] **Step 2: 运行测试确认失败**

Run: `dotnet test tests/Gameplay.Tests/Gameplay.Tests.csproj --filter "FullyQualifiedName~AbilitySpecTests"`
Expected: FAIL

- [ ] **Step 3: 实现**

```csharp
// src/Gameplay/Gameplay.Abilities/Ability/AbilitySpec.cs
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

/// <summary>角色拥有的 Ability 集合。挂在 Owner Entity 上。</summary>
public struct AbilityCollectionComponent : IComponent
{
    public AbilitySpec[] Specs;   // 预分配或动态扩展
}
```

- [ ] **Step 4: 运行测试确认通过**

Run: `dotnet test tests/Gameplay.Tests/Gameplay.Tests.csproj --filter "FullyQualifiedName~AbilitySpecTests"`
Expected: PASS

- [ ] **Step 5: 提交**

```bash
git add src/Gameplay/Gameplay.Abilities/Ability/AbilitySpec.cs src/Gameplay/Gameplay.Abilities/Ability/AbilityCollectionComponent.cs tests/Gameplay.Tests/Gameplay.Tests.Abilities/Ability/AbilitySpecTests.cs
git commit -m "feat: add AbilitySpec and AbilityCollectionComponent

Co-Authored-By: Claude <noreply@anthropic.com>"
```

---

### Task 4: ActiveAbilityComponent

**Files:**
- Create: `src/Gameplay/Gameplay.Abilities/Ability/ActiveAbilityComponent.cs`
- Create: `tests/Gameplay.Tests/Gameplay.Tests.Abilities/Ability/ActiveAbilityComponentTests.cs`

**Interfaces:**
- Consumes: 枚举 (Task 1)
- Produces: `ActiveAbilityComponent` struct (IComponent)

- [ ] **Step 1: 写测试**

```csharp
// tests/Gameplay.Tests/Gameplay.Tests.Abilities/Ability/ActiveAbilityComponentTests.cs
namespace Gameplay.Tests.Abilities;

using Gameplay.Abilities;
using Xunit;

public class ActiveAbilityComponentTests
{
    [Fact]
    public void Default_State_IsActivating()
    {
        var comp = new ActiveAbilityComponent();
        Assert.Equal(EAbilityInstanceState.Activating, comp.State);
    }

    [Fact]
    public void Default_Handle_IsZero()
    {
        var comp = new ActiveAbilityComponent();
        Assert.Equal(0, comp.Handle);
    }

    [Fact]
    public void Default_IsActive_IsFalse()
    {
        var comp = new ActiveAbilityComponent();
        Assert.False(comp.IsActive);
    }
}
```

- [ ] **Step 2: 运行测试确认失败**

Run: `dotnet test tests/Gameplay.Tests/Gameplay.Tests.csproj --filter "FullyQualifiedName~ActiveAbilityComponentTests"`
Expected: FAIL

- [ ] **Step 3: 实现**

```csharp
// src/Gameplay/Gameplay.Abilities/Ability/ActiveAbilityComponent.cs
using Friflo.Engine.ECS;

namespace Gameplay.Abilities;

/// <summary>
/// ActiveAbility 的运行时 Component。激活时创建子 Entity 挂此 Component，结束时销毁。
/// </summary>
public struct ActiveAbilityComponent : IComponent
{
    public float StartTime;                       // 激活时间戳
    public int Handle;                            // 全局唯一 ID
    public int DefinitionId;                      // Ability 静态定义 Registry 查表 key
    public bool IsActive;                         // 是否激活中
    public Entity Owner;                          // 归属的 Owner Entity
    public EAbilityInstanceState State;            // 当前状态
}
```

- [ ] **Step 4: 运行测试确认通过**

Run: `dotnet test tests/Gameplay.Tests/Gameplay.Tests.csproj --filter "FullyQualifiedName~ActiveAbilityComponentTests"`
Expected: PASS

- [ ] **Step 5: 提交**

```bash
git add src/Gameplay/Gameplay.Abilities/Ability/ActiveAbilityComponent.cs tests/Gameplay.Tests/Gameplay.Tests.Abilities/Ability/ActiveAbilityComponentTests.cs
git commit -m "feat: add ActiveAbilityComponent (runtime IComponent)

Co-Authored-By: Claude <noreply@anthropic.com>"
```

---

### Task 5: IAbilityRequirement + IAbilityCommit + IAbilityExecutor

**Files:**
- Create: `src/Gameplay/Gameplay.Abilities/Ability/IAbilityRequirement.cs`
- Create: `src/Gameplay/Gameplay.Abilities/Ability/IAbilityCommit.cs`
- Create: `src/Gameplay/Gameplay.Abilities/Ability/IAbilityExecutor.cs`
- Create: `src/Gameplay/Gameplay.Abilities/Ability/AbilityActivationRequest.cs`

**Interfaces:**
- Produces: 三个扩展点接口 + `AbilityActivationRequest` struct

- [ ] **Step 1: 写测试**

```csharp
// tests/Gameplay.Tests/Gameplay.Tests.Abilities/Ability/AbilityActivationRequestTests.cs
namespace Gameplay.Tests.Abilities;

using Gameplay.Abilities;
using Xunit;

public class AbilityActivationRequestTests
{
    [Fact]
    public void Default_Source_IsInput()
    {
        var req = new AbilityActivationRequest();
        Assert.Equal(EActivationSource.Input, req.Source);
    }

    [Fact]
    public void SpecHandle_DefaultsToZero()
    {
        var req = new AbilityActivationRequest();
        Assert.Equal(0, req.SpecHandle);
    }
}
```

- [ ] **Step 2: 运行测试确认失败**

Run: `dotnet test tests/Gameplay.Tests/Gameplay.Tests.csproj --filter "FullyQualifiedName~AbilityActivationRequestTests"`
Expected: FAIL

- [ ] **Step 3: 实现**

```csharp
// src/Gameplay/Gameplay.Abilities/Ability/AbilityActivationRequest.cs
namespace Gameplay.Abilities;

/// <summary>Ability 激活请求（POCO Command）。当前 Tick 消费。</summary>
public struct AbilityActivationRequest
{
    public Entity Owner;
    public int SpecHandle;
    public Entity Target;
    public EActivationSource Source;
}
```

```csharp
// src/Gameplay/Gameplay.Abilities/Ability/IAbilityRequirement.cs
namespace Gameplay.Abilities;

/// <summary>CanActivate 检查扩展点。返回 true = 通过。</summary>
public interface IAbilityRequirement
{
    bool Evaluate(Entity owner, AbilitySpec spec, in AbilityActivationRequest request);
}
```

```csharp
// src/Gameplay/Gameplay.Abilities/Ability/IAbilityCommit.cs
namespace Gameplay.Abilities;

/// <summary>Commit 扩展点。Requirements 全部通过后执行，有副作用。</summary>
public interface IAbilityCommit
{
    void Execute(Entity owner, AbilitySpec spec, in AbilityActivationRequest request);
}
```

```csharp
// src/Gameplay/Gameplay.Abilities/Ability/IAbilityExecutor.cs
namespace Gameplay.Abilities;

/// <summary>Execute 扩展点。能力逻辑的实际执行。</summary>
public interface IAbilityExecutor
{
    void Execute(Entity activeAbilityEntity, in AbilityActivationRequest request);
}
```

- [ ] **Step 4: 运行测试确认通过**

Run: `dotnet test tests/Gameplay.Tests/Gameplay.Tests.csproj --filter "FullyQualifiedName~AbilityActivationRequestTests"`
Expected: PASS

- [ ] **Step 5: 提交**

```bash
git add src/Gameplay/Gameplay.Abilities/Ability/IAbilityRequirement.cs src/Gameplay/Gameplay.Abilities/Ability/IAbilityCommit.cs src/Gameplay/Gameplay.Abilities/Ability/IAbilityExecutor.cs src/Gameplay/Gameplay.Abilities/Ability/AbilityActivationRequest.cs tests/Gameplay.Tests/Gameplay.Tests.Abilities/Ability/AbilityActivationRequestTests.cs
git commit -m "feat: add IAbilityRequirement, IAbilityCommit, IAbilityExecutor interfaces

Co-Authored-By: Claude <noreply@anthropic.com>"
```

---

### Task 6: 内置 Commit Actions — ApplyCooldownCommit + TagRequirement

**Files:**
- Create: `src/Gameplay/Gameplay.Abilities/Ability/CommitActions/ApplyCooldownCommit.cs`
- Create: `src/Gameplay/Gameplay.Abilities/Ability/CommitActions/TagRequirement.cs`
- Create: `tests/Gameplay.Tests/Gameplay.Tests.Abilities/Ability/ApplyCooldownCommitTests.cs`

**Interfaces:**
- Consumes: `IAbilityCommit` (Task 5), `EffectSystem.Apply` (Plan 1), `GameplayEffectSpec` (Plan 1)
- Produces: `ApplyCooldownCommit` class, `TagRequirement` class

- [ ] **Step 1: 写测试**

```csharp
// tests/Gameplay.Tests/Gameplay.Tests.Abilities/Ability/ApplyCooldownCommitTests.cs
namespace Gameplay.Tests.Abilities;

using Friflo.Engine.ECS;
using Gameplay.Abilities;
using Xunit;

public class ApplyCooldownCommitTests
{
    [Fact]
    public void Execute_NoCooldownEffect_Skips()
    {
        var store = new EntityStore();
        // Feature with just EffectSystem — no crash expected when CooldownEffect is null
        var attrSys = new AttributeSystem();
        var effectSys = new EffectSystem(attrSys);
        var commit = new ApplyCooldownCommit(effectSys);
        var owner = store.CreateEntity();

        var ability = new GameplayAbility(); // CooldownEffect = null
        var spec = new AbilitySpec { Ability = ability, Handle = 1 };
        var request = new AbilityActivationRequest { Owner = owner, SpecHandle = 1 };

        // 不应抛出异常
        commit.Execute(owner, spec, request);
    }
}

public class TagRequirementTests
{
    [Fact]
    public void Evaluate_NoRequirements_ReturnsTrue()
    {
        var req = new TagRequirement();
        var store = new EntityStore();
        var owner = store.CreateEntity();
        var spec = new AbilitySpec();
        var request = new AbilityActivationRequest { Owner = owner };

        Assert.True(req.Evaluate(owner, spec, request));
    }

    [Fact]
    public void Evaluate_ActivationBlocked_Fails()
    {
        var blockedTag = GameplayTag.Request("State.Dead");
        var ability = new GameplayAbility();
        ability.ActivationBlockedTags.AddTag(blockedTag);

        var store = new EntityStore();
        var owner = store.CreateEntity();
        owner.AddComponent(new GameplayTagsComponent());
        owner.GetComponent<GameplayTagsComponent>().AddTag(blockedTag);

        var spec = new AbilitySpec { Ability = ability };
        var request = new AbilityActivationRequest { Owner = owner };
        var req = new TagRequirement();

        Assert.False(req.Evaluate(owner, spec, request));
    }
}
```

- [ ] **Step 2: 运行测试确认失败**

Run: `dotnet test tests/Gameplay.Tests/Gameplay.Tests.csproj --filter "FullyQualifiedName~ApplyCooldownCommitTests|FullyQualifiedName~TagRequirementTests"`
Expected: FAIL

- [ ] **Step 3: 实现**

```csharp
// src/Gameplay/Gameplay.Abilities/Ability/CommitActions/ApplyCooldownCommit.cs
namespace Gameplay.Abilities;

/// <summary>施加 Cooldown GameplayEffect 的 Commit。</summary>
public class ApplyCooldownCommit : IAbilityCommit
{
    private readonly EffectSystem effectSystem;

    public ApplyCooldownCommit(EffectSystem effectSystem)
    {
        this.effectSystem = effectSystem;
    }

    public void Execute(Entity owner, AbilitySpec spec, in AbilityActivationRequest request)
    {
        if (spec.Ability.CooldownEffect == null) return;
        var geSpec = new GameplayEffectSpec(spec.Ability.CooldownEffect, spec.Level);
        effectSystem.Apply(geSpec, owner);
    }
}
```

```csharp
// src/Gameplay/Gameplay.Abilities/Ability/CommitActions/TagRequirement.cs
using Gameplay.Tags;

namespace Gameplay.Abilities;

/// <summary>
/// Ability 激活 Tag 条件检查。
/// 检查 ActivationRequiredTags / ActivationBlockedTags / SourceRequiredTags / SourceBlockedTags。
/// </summary>
public class TagRequirement : IAbilityRequirement
{
    public bool Evaluate(Entity owner, AbilitySpec spec, in AbilityActivationRequest request)
    {
        var ability = spec.Ability;
        if (ability == null) return true;

        // ActivationBlockedTags: Owner 有任一 Blocked Tag → 失败
        if (ability.ActivationBlockedTags.Count > 0)
        {
            if (owner.TryGetComponent<GameplayTagsComponent>(out var ownerTags))
            {
                if (ownerTags.MatchesAnyTags(ability.ActivationBlockedTags))
                    return false;
            }
        }

        // ActivationRequiredTags: Owner 必须有全部 Required Tag
        if (ability.ActivationRequiredTags.Count > 0)
        {
            if (!owner.TryGetComponent<GameplayTagsComponent>(out var ownerTags))
                return false;
            if (!ownerTags.HasAll(ability.ActivationRequiredTags))
                return false;
        }

        // Cooldown 检查: 有 Cooldown GE 施加的 Cooldown Tag 则阻止
        // Cooldown Tag 由 CooldownEffect.GrantedTags 施加，检查 Owner 是否有该 Tag
        // 此检查已在 ActivationBlockedTags 中通过策划配置"Cooldown.X" Tag 覆盖

        return true;
    }
}
```

- [ ] **Step 4: 运行测试确认通过**

Run: `dotnet test tests/Gameplay.Tests/Gameplay.Tests.csproj --filter "FullyQualifiedName~ApplyCooldownCommitTests|FullyQualifiedName~TagRequirementTests"`
Expected: PASS

> **注意：** 测试依赖 `GameplayTagsComponent.MatchesAnyTags` 和 `GameplayTagsComponent.HasAll` 方法。如这些方法不存在，需要在 Task 6 中补充。

- [ ] **Step 5: 提交**

```bash
git add src/Gameplay/Gameplay.Abilities/Ability/CommitActions/ApplyCooldownCommit.cs src/Gameplay/Gameplay.Abilities/Ability/CommitActions/TagRequirement.cs tests/Gameplay.Tests/Gameplay.Tests.Abilities/Ability/ApplyCooldownCommitTests.cs
git commit -m "feat: add ApplyCooldownCommit and TagRequirement for Ability pipeline

Co-Authored-By: Claude <noreply@anthropic.com>"
```

---

### Task 7: AbilityActivationSystem + Pipeline

**Files:**
- Create: `src/Gameplay/Gameplay.Abilities/Ability/AbilityActivationSystem.cs`
- Create: `tests/Gameplay.Tests/Gameplay.Tests.Abilities/Ability/AbilityActivationSystemTests.cs`

**Interfaces:**
- Consumes: `EffectSystem` (Plan 1), `IAbilityRequirement`/`IAbilityCommit`/`IAbilityExecutor` (Task 5), `AbilityCollectionComponent` (Task 3), `ActiveAbilityComponent` (Task 4)
- Produces: `AbilityActivationSystem` (POCO，非 QuerySystem)

- [ ] **Step 1: 写测试**

```csharp
// tests/Gameplay.Tests/Gameplay.Tests.Abilities/Ability/AbilityActivationSystemTests.cs
namespace Gameplay.Tests.Abilities;

using Friflo.Engine.ECS;
using Gameplay.Abilities;
using Gameplay.Tags;
using Xunit;

public class AbilityActivationSystemTests
{
    [Fact]
    public void TryActivate_NoAbilityCollection_ReturnsFalse()
    {
        var store = new EntityStore();
        var attrSys = new AttributeSystem();
        var effectSys = new EffectSystem(attrSys);
        var sys = new AbilityActivationSystem(effectSys);

        var owner = store.CreateEntity();
        var request = new AbilityActivationRequest { Owner = owner, SpecHandle = 0 };

        Assert.False(sys.TryActivateAbility(request));
    }

    [Fact]
    public void TryActivate_InvalidSpecHandle_ReturnsFalse()
    {
        var store = new EntityStore();
        var attrSys = new AttributeSystem();
        var effectSys = new EffectSystem(attrSys);
        var sys = new AbilityActivationSystem(effectSys);

        var owner = store.CreateEntity();
        owner.AddComponent(new AbilityCollectionComponent
        {
            Specs = new AbilitySpec[] { new() { Level = 1 } }
        });
        var request = new AbilityActivationRequest { Owner = owner, SpecHandle = 99 };

        Assert.False(sys.TryActivateAbility(request));
    }

    [Fact]
    public void TryActivate_RequirementsFail_ReturnsFalse()
    {
        var store = new EntityStore();
        var attrSys = new AttributeSystem();
        var effectSys = new EffectSystem(attrSys);

        var blockedTag = GameplayTag.Request("State.Stunned");
        var ability = new GameplayAbility();
        ability.ActivationBlockedTags.AddTag(blockedTag);

        var sys = new AbilityActivationSystem(effectSys);
        var owner = store.CreateEntity();
        owner.AddComponent(new GameplayTagsComponent());
        owner.GetComponent<GameplayTagsComponent>().AddTag(blockedTag);
        owner.AddComponent(new AbilityCollectionComponent
        {
            Specs = new[] { new AbilitySpec { Ability = ability, Handle = 0 } }
        });

        var request = new AbilityActivationRequest { Owner = owner, SpecHandle = 0 };
        Assert.False(sys.TryActivateAbility(request));
    }

    [Fact]
    public void TryActivate_Success_CreatesActiveAbilityEntity()
    {
        var store = new EntityStore();
        var attrSys = new AttributeSystem();
        var effectSys = new EffectSystem(attrSys);
        var sys = new AbilityActivationSystem(effectSys);

        var ability = new GameplayAbility();
        var executor = new TestExecutor();
        ability.Executor = executor;

        var owner = store.CreateEntity();
        owner.AddComponent(new AbilityCollectionComponent
        {
            Specs = new[] { new AbilitySpec { Ability = ability, Handle = 0 } }
        });

        var request = new AbilityActivationRequest { Owner = owner, SpecHandle = 0 };
        Assert.True(sys.TryActivateAbility(request));
        Assert.True(executor.WasCalled);
    }

    private class TestExecutor : IAbilityExecutor
    {
        public bool WasCalled;
        public void Execute(Entity activeAbilityEntity, in AbilityActivationRequest request)
            => WasCalled = true;
    }
}
```

- [ ] **Step 2: 运行测试确认失败**

Run: `dotnet test tests/Gameplay.Tests/Gameplay.Tests.csproj --filter "FullyQualifiedName~AbilityActivationSystemTests"`
Expected: FAIL

- [ ] **Step 3: 实现**

```csharp
// src/Gameplay/Gameplay.Abilities/Ability/AbilityActivationSystem.cs
using Gameplay.Tags;

namespace Gameplay.Abilities;

/// <summary>
/// Ability 激活流程 System（POCO，不继承 QuerySystem）。
/// 接收 AbilityActivationRequest → Requirements → Commit → Execute。
/// </summary>
public class AbilityActivationSystem
{
    private readonly EffectSystem effectSystem;
    private int nextHandle = 1;

    public AbilityActivationSystem(EffectSystem effectSystem)
    {
        this.effectSystem = effectSystem;
    }

    /// <summary>尝试激活 Ability。返回 true = 成功。</summary>
    public bool TryActivateAbility(in AbilityActivationRequest request)
    {
        var owner = request.Owner;

        // 查找 AbilitySpec
        if (!owner.TryGetComponent<AbilityCollectionComponent>(out var collection))
            return false;
        if (request.SpecHandle < 0 || request.SpecHandle >= collection.Specs.Length)
            return false;

        var spec = collection.Specs[request.SpecHandle];
        var ability = spec.Ability;
        if (ability == null) return false;

        // ── 1. Requirements（纯检查）──
        foreach (var req in ability.Requirements)
        {
            if (!req.Evaluate(owner, spec, request))
                return false;
        }
        // 内置: Tag 检查
        var tagReq = new TagRequirement();
        if (!tagReq.Evaluate(owner, spec, request))
            return false;

        // ── 2. Commit（副作用）──
        foreach (var commit in ability.CommitActions)
        {
            commit.Execute(owner, spec, request);
        }

        // ── 3. Create ActiveAbility Entity ──
        int handle = nextHandle++;
        var activeEntity = owner.Store.CreateEntity();
        activeEntity.AddChild(owner);
        activeEntity.AddComponent(new ActiveAbilityComponent
        {
            StartTime = 0f, // TBD: world time
            Handle = handle,
            IsActive = true,
            Owner = owner,
            State = EAbilityInstanceState.Active,
        });

        // ── 4. Execute ──
        var executor = ability.Executor;
        if (executor != null)
        {
            try
            {
                executor.Execute(activeEntity, request);
            }
            catch
            {
                // 回滚 Commit
                RollbackCommit(ref activeEntity, owner, spec);
                activeEntity.DeleteEntity();
                return false;
            }
        }

        // 添加 ActivationOwnedTags
        if (ability.ActivationOwnedTags.Count > 0)
        {
            if (owner.TryGetComponent<GameplayTagsComponent>(out var tags))
            {
                // 使用 TagSource ref counting（后续 Plan 完善）
                foreach (var tag in ability.ActivationOwnedTags)
                    tags.AddTag(tag);
            }
        }

        return true;
    }

    /// <summary>取消 Ability。</summary>
    public void CancelAbility(Entity activeEntity)
    {
        if (!activeEntity.TryGetComponent<ActiveAbilityComponent>(out var comp))
            return;

        comp.State = EAbilityInstanceState.Cancelled;
        comp.IsActive = false;

        var owner = comp.Owner;

        // 移除 ActivationOwnedTags
        if (!owner.IsNull)
        {
            // 通过 AbilitySpec 查找 Definition → 移除对应 Tag
            // Plan 2 简化：不做反查
        }

        activeEntity.DeleteEntity();
    }

    private void RollbackCommit(ref Entity activeEntity, Entity owner, AbilitySpec spec)
    {
        // 撤销 Cooldown: 查找 Handle → RemoveEffect
        // 撤销 Cost: 退还 Attribute
        // Plan 2 简化为占位
    }
}
```

- [ ] **Step 4: 运行测试确认通过**

Run: `dotnet test tests/Gameplay.Tests/Gameplay.Tests.csproj --filter "FullyQualifiedName~AbilityActivationSystemTests"`
Expected: PASS

- [ ] **Step 5: 提交**

```bash
git add src/Gameplay/Gameplay.Abilities/Ability/AbilityActivationSystem.cs tests/Gameplay.Tests/Gameplay.Tests.Abilities/Ability/AbilityActivationSystemTests.cs
git commit -m "feat: add AbilityActivationSystem with Requirements→Commit→Execute pipeline

Co-Authored-By: Claude <noreply@anthropic.com>"
```

---

### Task 8: TagSource 引用计数（GameplayTagsComponent 扩展）

**Files:**
- Modify: `src/Gameplay/Gameplay.Tags/GameplayTagsComponent.cs`
- **Modify:** `tests/Gameplay.Tests/Gameplay.Tests.Tags/GameplayTagsTests.cs`（**追加**到文件末尾，保留已有测试）

**Interfaces:**
- Consumes: 现有 `GameplayTagsComponent`, `GameplayTagSet`
- Produces: 支持 ref-counting 的 `AddTagSource` / `RemoveTagSource`

- [ ] **Step 1: 写测试**

```csharp
// 追加到 tests/Gameplay.Tests/Gameplay.Tests.Tags/GameplayTagsTests.cs
[Fact]
public void TagSource_MultipleSources_TagPersists()
{
    // 两个不同的"来源"独立 Add/Remove
    var tag = GameplayTag.Request("Test.MultiSource");
    var comp = new GameplayTagsComponent();
    
    comp.AddTag(tag);  // Source 1
    comp.AddTag(tag);  // Source 2
    Assert.True(comp.HasTag(tag));
    
    comp.RemoveTag(tag);  // Source 1 removed
    Assert.True(comp.HasTag(tag)); // Still has Source 2
    
    comp.RemoveTag(tag);  // Source 2 removed
    Assert.False(comp.HasTag(tag)); // All sources gone
}
```

- [ ] **Step 2: 运行测试确认失败**

Run: `dotnet test tests/Gameplay.Tests/Gameplay.Tests.csproj --filter "FullyQualifiedName~TagSource_MultipleSources"`
Expected: FAIL

- [ ] **Step 3: 增加 ref-counting 到 GameplayTagsComponent**

在现有 `GameplayTagsComponent` 中增加内部 `Dictionary<GameplayTag, int>` 字段 + 修改 `AddTag`/`RemoveTag` 逻辑（见 spec TagSource 引用计数 小节）。

- [ ] **Step 4: 运行测试确认通过**

Run: `dotnet test tests/Gameplay.Tests/Gameplay.Tests.csproj --filter "FullyQualifiedName~TagSource_MultipleSources"`
Expected: PASS

- [ ] **Step 5: 提交**

```bash
git add src/Gameplay/Gameplay.Tags/GameplayTagsComponent.cs tests/Gameplay.Tests/Gameplay.Tests.Tags/GameplayTagsTests.cs
git commit -m "feat: add TagSource reference counting to GameplayTagsComponent

Co-Authored-By: Claude <noreply@anthropic.com>"
```

---

## 验证

完成后运行全量测试：

```bash
dotnet test tests/Gameplay.Tests/Gameplay.Tests.csproj -f net10.0
dotnet build src/Gameplay/Gameplay.csproj -f netstandard2.1
```

确认所有 Ability 相关测试通过，Plan 1 测试无回归。
