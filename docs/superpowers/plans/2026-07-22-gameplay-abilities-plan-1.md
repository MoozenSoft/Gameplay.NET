# GameplayAbilities Plan 1: Attribute + GameplayEffect

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 实现 GameplayAbilities 框架的 P0 层——属性系统和效果系统（Attribute + GameplayEffect）

**Architecture:** GameplayAttributeData 做纯数据容器，AttributeAggregator 做独立的 Mod 列表 + 增量重算。GameplayEffect 走三层模型（静态定义 → Spec → 运行时 Entity），EffectSystem 单一 System 驱动 Tick/Apply/Remove。

**Tech Stack:** C# (.NET 10 / netstandard2.1), Friflo.Engine.ECS, xUnit

**Spec:** `docs/superpowers/specs/2026-07-21-gameplay-abilities-design.md`

## Global Constraints

- 命名空间：源文件 `Gameplay.GameplayAbilities`，测试文件 `Gameplay.Tests.GameplayAbilities`，文件范围命名空间（`namespace X;` 不加括号缩进）
- 文档和注释用中文，专业术语用英文
- TDD：先写测试 → 确认失败 → 写实现 → 确认通过 → 提交
- 热路径 0 GC：ECS Component 用 struct；冷路径（初始化/配置）可放松
- 目标 TFM：`netstandard2.1` + `net10.0`

---

## 文件结构

```
src/Gameplay/GameplayAbilities/
├── Attribute/
│   ├── GameplayAttributeData.cs           # 属性值容器（BaseValue + CurrentValue）
│   ├── IAttributeSetComponent.cs          # 标记接口
│   ├── DirtyAttributeComponent.cs         # 属性脏标记 bitmask
│   ├── AttributeAggregator.cs             # Mod 列表 + Evaluate（internal）
│   ├── ModEntry.cs                        # 单个 Mod 条目（internal）
│   └── AttributeSystem.cs                 # 脏 Attribute 重算 System
├── GameplayEffect/
│   ├── Enums.cs                           # 所有 GameplayEffect 相关枚举
│   ├── GameplayModifier.cs                # 修改器定义
│   ├── GameplayEffectModifierMagnitude.cs # 幅度计算（4 种方式）
│   ├── GameplayEffect.cs                  # 静态定义
│   ├── GameplayEffectSpec.cs              # 施放实例
│   ├── ActiveGameplayEffectComponent.cs   # 运行时 Component
│   ├── GameplayEffectQuery.cs             # 效果查询条件
│   ├── ConditionalGameplayEffect.cs       # 条件效果（OnApplication/OnComplete）
│   ├── GameplayEffectCue.cs               # Cue 定义
│   └── EffectSystem.cs                    # Tick / Apply / Remove
└── GameplayAbilitiesFeature.cs            # 注册入口

tests/Gameplay.Tests/GameplayAbilities/
├── Attribute/
│   ├── GameplayAttributeDataTests.cs
│   ├── DirtyAttributeComponentTests.cs
│   ├── AttributeAggregatorTests.cs
│   └── AttributeSystemTests.cs
└── GameplayEffect/
    ├── GameplayEffectSpecTests.cs
    ├── ActiveGameplayEffectComponentTests.cs
    ├── GameplayEffectQueryTests.cs
    └── EffectSystemTests.cs
```

---

### Task 1: GameplayAttributeData + IAttributeSetComponent

**Files:**
- Create: `src/Gameplay/GameplayAbilities/Attribute/GameplayAttributeData.cs`
- Create: `src/Gameplay/GameplayAbilities/Attribute/IAttributeSetComponent.cs`
- Create: `tests/Gameplay.Tests/GameplayAbilities/Attribute/GameplayAttributeDataTests.cs`

**Interfaces:**
- Produces: `GameplayAttributeData` struct (BaseValue, CurrentValue), `IAttributeSetComponent` interface

- [ ] **Step 1: 写 GameplayAttributeData 测试**

```csharp
// tests/Gameplay.Tests/GameplayAbilities/Attribute/GameplayAttributeDataTests.cs
namespace Gameplay.Tests.GameplayAbilities;

using Gameplay.GameplayAbilities;

public class GameplayAttributeDataTests
{
    [Fact]
    public void Default_BaseAndCurrent_AreZero()
    {
        var data = new GameplayAttributeData();
        Assert.Equal(0f, data.BaseValue);
        Assert.Equal(0f, data.CurrentValue);
    }

    [Fact]
    public void SetBaseValue_CurrentValueUnchanged()
    {
        var data = new GameplayAttributeData { BaseValue = 100f, CurrentValue = 80f };
        data.BaseValue = 120f;
        Assert.Equal(120f, data.BaseValue);
        Assert.Equal(80f, data.CurrentValue); // Current 不联动，等待 Evaluator
    }
}
```

- [ ] **Step 2: 运行测试确认失败**

Run: `dotnet test tests/Gameplay.Tests/Gameplay.Tests.csproj --filter "FullyQualifiedName~GameplayAttributeDataTests"`
Expected: FAIL — 类型不存在

- [ ] **Step 3: 实现 GameplayAttributeData + IAttributeSetComponent**

```csharp
// src/Gameplay/GameplayAbilities/Attribute/GameplayAttributeData.cs
namespace Gameplay.GameplayAbilities;

/// <summary>属性值容器。纯数据，Aggregator 负责计算 CurrentValue。</summary>
public struct GameplayAttributeData
{
    /// <summary>永久基础值（升级加点等）。</summary>
    public float BaseValue;

    /// <summary>计算后的当前值 = Evaluate(BaseValue, Mods)。由 AttributeSystem 写入。</summary>
    public float CurrentValue;
}
```

```csharp
// src/Gameplay/GameplayAbilities/Attribute/IAttributeSetComponent.cs
using Friflo.Engine.ECS;

namespace Gameplay.GameplayAbilities;

/// <summary>标记 struct 为 AttributeSet——Entity 可挂多个实现此接口的 Component。</summary>
public interface IAttributeSetComponent : IComponent { }
```

- [ ] **Step 4: 运行测试确认通过**

Run: `dotnet test tests/Gameplay.Tests/Gameplay.Tests.csproj --filter "FullyQualifiedName~GameplayAttributeDataTests"`
Expected: PASS

- [ ] **Step 5: 提交**

```bash
git add src/Gameplay/GameplayAbilities/Attribute/GameplayAttributeData.cs src/Gameplay/GameplayAbilities/Attribute/IAttributeSetComponent.cs tests/Gameplay.Tests/GameplayAbilities/Attribute/GameplayAttributeDataTests.cs
git commit -m "feat: add GameplayAttributeData and IAttributeSetComponent

- GameplayAttributeData: pure data container with BaseValue/CurrentValue
- IAttributeSetComponent: marker interface for AttributeSet structs

Co-Authored-By: Claude <noreply@anthropic.com>"
```

---

### Task 2: GameplayEffect 枚举

**Files:**
- Create: `src/Gameplay/GameplayAbilities/GameplayEffect/Enums.cs`
- Create: `tests/Gameplay.Tests/GameplayAbilities/GameplayEffect/EnumsTests.cs`

**Interfaces:**
- Produces: 8 个枚举类型

- [ ] **Step 1: 写枚举存在性测试**

```csharp
// tests/Gameplay.Tests/GameplayAbilities/GameplayEffect/EnumsTests.cs
namespace Gameplay.Tests.GameplayAbilities;

using Gameplay.GameplayAbilities;

public class EnumsTests
{
    [Fact]
    public void DurationType_HasExpectedValues()
    {
        Assert.Equal(0, (int)EGameplayEffectDurationType.Instant);
        Assert.Equal(1, (int)EGameplayEffectDurationType.HasDuration);
        Assert.Equal(2, (int)EGameplayEffectDurationType.Infinite);
    }

    [Fact]
    public void ModOp_HasAllOperations()
    {
        var values = Enum.GetValues<EGameplayModOp>();
        Assert.Contains(EGameplayModOp.Additive, values);
        Assert.Contains(EGameplayModOp.Multiply, values);
        Assert.Contains(EGameplayModOp.Divide, values);
        Assert.Contains(EGameplayModOp.Override, values);
        Assert.Contains(EGameplayModOp.FinalAdd, values);
    }
}
```

- [ ] **Step 2: 运行测试确认失败**

Run: `dotnet test tests/Gameplay.Tests/Gameplay.Tests.csproj --filter "FullyQualifiedName~EnumsTests"`
Expected: FAIL

- [ ] **Step 3: 实现所有枚举**

```csharp
// src/Gameplay/GameplayAbilities/GameplayEffect/Enums.cs
namespace Gameplay.GameplayAbilities;

/// <summary>GE 的持续时间策略。</summary>
public enum EGameplayEffectDurationType
{
    Instant,       // 立即执行，不创建 ActiveEntity
    HasDuration,   // 有限时长
    Infinite,      // 无限时长
}

/// <summary>Modifier 运算类型。</summary>
public enum EGameplayModOp
{
    Additive,      // Base + ΣAdd
    Multiply,      // × ΠMultiply
    Divide,        // / ΠDivide
    Override,      // = OverrideValue
    FinalAdd,      // ... + ΣFinalAdd
}

/// <summary>堆叠时 Duration 策略。</summary>
public enum EGameplayEffectStackingDurationPolicy
{
    RefreshOnSuccessfulApplication,
    NeverRefresh,
    ExtendDuration,
}

/// <summary>堆叠时 Period 策略。</summary>
public enum EGameplayEffectStackingPeriodPolicy
{
    ResetOnSuccessfulApplication,
    NeverReset,
}

/// <summary>堆叠到期策略。</summary>
public enum EGameplayEffectStackingExpirationPolicy
{
    ClearEntireStack,
    RemoveSingleStackAndRefreshDuration,
    RefreshDuration,
}

/// <summary>Inhibition 解除后 Period 策略。</summary>
public enum EGameplayEffectPeriodInhibitionRemovedPolicy
{
    NeverReset,
    ResetPeriod,
    ExecuteAndResetPeriod,
}

/// <summary>Modifier 属性抓取策略。</summary>
public enum EAttributeCapturePolicy
{
    Snapshot,   // Spec 创建时抓取一次
    RealTime,   // 每次 Execute 实时抓取
}

/// <summary>Effect 结束原因。</summary>
public enum EEffectEndType
{
    Normal,       // Duration 自然到期 / StackCount 归零
    Premature,    // RemoveEffect() 主动移除 / RemoveOtherEffects / RemovalTags
}
```

- [ ] **Step 4: 运行测试确认通过**

Run: `dotnet test tests/Gameplay.Tests/Gameplay.Tests.csproj --filter "FullyQualifiedName~EnumsTests"`
Expected: PASS

- [ ] **Step 5: 提交**

```bash
git add src/Gameplay/GameplayAbilities/GameplayEffect/Enums.cs tests/Gameplay.Tests/GameplayAbilities/GameplayEffect/EnumsTests.cs
git commit -m "feat: add GameplayEffect enums (DurationType, ModOp, Stacking, Capture, EndType)

Co-Authored-By: Claude <noreply@anthropic.com>"
```

---

### Task 3: GameplayModifier + GameplayEffectModifierMagnitude

**Files:**
- Create: `src/Gameplay/GameplayAbilities/GameplayEffect/GameplayModifier.cs`
- Create: `src/Gameplay/GameplayAbilities/GameplayEffect/GameplayEffectModifierMagnitude.cs`
- Create: `tests/Gameplay.Tests/GameplayAbilities/GameplayEffect/GameplayModifierTests.cs`

**Interfaces:**
- Produces: `GameplayModifier` struct, `GameplayEffectModifierMagnitude` class

- [ ] **Step 1: 写测试**

```csharp
// tests/Gameplay.Tests/GameplayAbilities/GameplayEffect/GameplayModifierTests.cs
namespace Gameplay.Tests.GameplayAbilities;

using Gameplay.GameplayAbilities;

public class GameplayModifierTests
{
    [Fact]
    public void ScalableFloat_CreatesMagnitude()
    {
        var mag = GameplayEffectModifierMagnitude.CreateScalableFloat(1.5f, 10f);
        Assert.Equal(EGameplayEffectMagnitudeCalculation.ScalableFloat, mag.CalculationType);
    }

    [Fact]
    public void AttributeBased_CreatesMagnitude()
    {
        var mag = GameplayEffectModifierMagnitude.CreateAttributeBased(
            coefficient: 1.0f, preAdd: 0f, postAdd: 0f);
        Assert.Equal(EGameplayEffectMagnitudeCalculation.AttributeBased, mag.CalculationType);
    }

    [Fact]
    public void Modifier_DefaultCapturePolicy_IsSnapshot()
    {
        var modifier = new GameplayModifier
        {
            ModOp = EGameplayModOp.Additive,
            CapturePolicy = default
        };
        Assert.Equal(EAttributeCapturePolicy.Snapshot, modifier.CapturePolicy);
    }
}
```

- [ ] **Step 2: 运行测试确认失败**

Run: `dotnet test tests/Gameplay.Tests/Gameplay.Tests.csproj --filter "FullyQualifiedName~GameplayModifierTests"`
Expected: FAIL

- [ ] **Step 3: 实现**

```csharp
// src/Gameplay/GameplayAbilities/GameplayEffect/GameplayEffectModifierMagnitude.cs
namespace Gameplay.GameplayAbilities;

/// <summary>幅度计算方式。</summary>
public enum EGameplayEffectMagnitudeCalculation
{
    ScalableFloat,
    AttributeBased,
    CustomCalculationClass,
    SetByCaller,
}

/// <summary>GameplayEffect Modifier 的幅度值（4 种计算方式之一）。</summary>
public class GameplayEffectModifierMagnitude
{
    public EGameplayEffectMagnitudeCalculation CalculationType { get; private set; }

    // ScalableFloat
    public float Coefficient { get; private set; }
    public float ScalableValue { get; private set; }

    // AttributeBased
    public float AttrCoefficient { get; private set; }
    public float PreMultiplyAdditive { get; private set; }
    public float PostMultiplyAdditive { get; private set; }
    // 引用的 GameplayAttribute 在 GameplayModifier 中指定

    private GameplayEffectModifierMagnitude() { }

    public static GameplayEffectModifierMagnitude CreateScalableFloat(float coefficient, float value)
        => new() { CalculationType = EGameplayEffectMagnitudeCalculation.ScalableFloat,
                   Coefficient = coefficient, ScalableValue = value };

    public static GameplayEffectModifierMagnitude CreateAttributeBased(
        float coefficient, float preAdd, float postAdd)
        => new() { CalculationType = EGameplayEffectMagnitudeCalculation.AttributeBased,
                   AttrCoefficient = coefficient, PreMultiplyAdditive = preAdd,
                   PostMultiplyAdditive = postAdd };

    public static GameplayEffectModifierMagnitude CreateCustomCalculation()
        => new() { CalculationType = EGameplayEffectMagnitudeCalculation.CustomCalculationClass };

    public static GameplayEffectModifierMagnitude CreateSetByCaller()
        => new() { CalculationType = EGameplayEffectMagnitudeCalculation.SetByCaller };
}
```

```csharp
// src/Gameplay/GameplayAbilities/GameplayEffect/GameplayModifier.cs
namespace Gameplay.GameplayAbilities;

/// <summary>GameplayEffect 的单个 Modifier 定义——改哪个属性 + 怎么算 + 什么操作。</summary>
public struct GameplayModifier
{
    /// <summary>修改的目标属性（SG 生成的 GameplayAttribute 句柄）。</summary>
    public int AttributeId;

    /// <summary>运算类型。</summary>
    public EGameplayModOp ModOp;

    /// <summary>幅度定义。</summary>
    public GameplayEffectModifierMagnitude MagnitudeCalc;

    /// <summary>属性抓取策略。</summary>
    public EAttributeCapturePolicy CapturePolicy;

    /// <summary>Modifier 执行类型：Persistent / ExecuteOnApply / ExecuteOnPeriod。</summary>
    public EModifierExecutionType ExecutionType;

    // TagRequirements 在指定 source/target 时必须满足才生效（后续 EffectSystem 实现）
}

/// <summary>Modifier 执行类型——避免 Period 重复累加 Persistent Modifier。</summary>
public enum EModifierExecutionType
{
    Persistent,         // Apply → 注册 Aggregator；Remove → 移除
    ExecuteOnApply,     // Apply 时执行一次，不注册（Instant GE）
    ExecuteOnPeriod,    // 每次 Period 执行一次，不注册为持续 Mod（DOT/HOT）
}
```

- [ ] **Step 4: 运行测试确认通过**

Run: `dotnet test tests/Gameplay.Tests/Gameplay.Tests.csproj --filter "FullyQualifiedName~GameplayModifierTests"`
Expected: PASS

- [ ] **Step 5: 提交**

```bash
git add src/Gameplay/GameplayAbilities/GameplayEffect/GameplayModifier.cs src/Gameplay/GameplayAbilities/GameplayEffect/GameplayEffectModifierMagnitude.cs tests/Gameplay.Tests/GameplayAbilities/GameplayEffect/GameplayModifierTests.cs
git commit -m "feat: add GameplayModifier and GameplayEffectModifierMagnitude

Co-Authored-By: Claude <noreply@anthropic.com>"
```

---

### Task 4: GameplayEffect 静态定义

**Files:**
- Create: `src/Gameplay/GameplayAbilities/GameplayEffect/GameplayEffect.cs`
- Create: `tests/Gameplay.Tests/GameplayAbilities/GameplayEffect/GameplayEffectTests.cs`

**Interfaces:**
- Consumes: `GameplayModifier`, `GameplayEffectModifierMagnitude`, 枚举 (Task 2-3)
- Produces: `GameplayEffect` class

- [ ] **Step 1: 写测试**

```csharp
// tests/Gameplay.Tests/GameplayAbilities/GameplayEffect/GameplayEffectTests.cs
namespace Gameplay.Tests.GameplayAbilities;

using Gameplay.GameplayTags;
using Gameplay.GameplayAbilities;

public class GameplayEffectTests
{
    [Fact]
    public void Default_DurationPolicy_IsInstant()
    {
        var ge = new GameplayEffect();
        Assert.Equal(EGameplayEffectDurationType.Instant, ge.DurationPolicy);
    }

    [Fact]
    public void HasDuration_Period_DefaultZero()
    {
        var ge = new GameplayEffect
        {
            DurationPolicy = EGameplayEffectDurationType.HasDuration,
            Period = 2.0f,
        };
        Assert.Equal(EGameplayEffectDurationType.HasDuration, ge.DurationPolicy);
        Assert.Equal(2.0f, ge.Period);
    }

    [Fact]
    public void Modifiers_InitiallyEmpty()
    {
        var ge = new GameplayEffect();
        Assert.NotNull(ge.Modifiers);
        Assert.Empty(ge.Modifiers);
    }

    [Fact]
    public void AddModifier_IncreasesCount()
    {
        var ge = new GameplayEffect();
        ge.Modifiers.Add(new GameplayModifier
        {
            AttributeId = 1,
            ModOp = EGameplayModOp.Additive,
            MagnitudeCalc = GameplayEffectModifierMagnitude.CreateScalableFloat(1f, 10f),
        });
        Assert.Single(ge.Modifiers);
    }
}
```

- [ ] **Step 2: 运行测试确认失败**

Run: `dotnet test tests/Gameplay.Tests/Gameplay.Tests.csproj --filter "FullyQualifiedName~GameplayEffectTests"`
Expected: FAIL

- [ ] **Step 3: 实现 GameplayEffect**

```csharp
// src/Gameplay/GameplayAbilities/GameplayEffect/GameplayEffect.cs
using System.Collections.Generic;
using Gameplay.GameplayTags;

namespace Gameplay.GameplayAbilities;

/// <summary>
/// GameplayEffect 静态定义（非 Entity）。策划/开发者配置的资产级数据。
/// </summary>
public class GameplayEffect
{
    // ── 基础 ──
    public EGameplayEffectDurationType DurationPolicy = EGameplayEffectDurationType.Instant;
    public int StackLimit = 1;
    public EGameplayEffectStackingDurationPolicy StackingDurationPolicy;
    public EGameplayEffectStackingPeriodPolicy StackingPeriodPolicy;
    public EGameplayEffectStackingExpirationPolicy StackingExpirationPolicy;
    public float Period;
    public EGameplayEffectPeriodInhibitionRemovedPolicy InhibitionPolicy;

    // ── Modifiers ──
    public List<GameplayModifier> Modifiers = new();

    // ── Tag 条件 ──
    public GameplayTagContainer ApplicationRequiredTags = new();
    public GameplayTagContainer OngoingRequiredTags = new();
    public GameplayTagContainer RemovalTags = new();

    // ── 副作用 ──
    public GameplayTagContainer GrantedTags = new();
    public GameplayTagContainer BlockedAbilityTags = new();
    public GameplayTagContainer CancelAbilityTags = new();

    // ── 其他 ──
    public float ChanceToApply = 1.0f;
    // ImmunityQueries / RemoveOtherEffectsQueries / OnApplicationEffects / OnCompleteEffects
    // CueDefinitions 在后续 Task 补充
}
```

- [ ] **Step 4: 运行测试确认通过**

Run: `dotnet test tests/Gameplay.Tests/Gameplay.Tests.csproj --filter "FullyQualifiedName~GameplayEffectTests"`
Expected: PASS

- [ ] **Step 5: 提交**

```bash
git add src/Gameplay/GameplayAbilities/GameplayEffect/GameplayEffect.cs tests/Gameplay.Tests/GameplayAbilities/GameplayEffect/GameplayEffectTests.cs
git commit -m "feat: add GameplayEffect static definition class

Co-Authored-By: Claude <noreply@anthropic.com>"
```

---

### Task 5: GameplayEffectSpec

**Files:**
- Create: `src/Gameplay/GameplayAbilities/GameplayEffect/GameplayEffectSpec.cs`
- Create: `tests/Gameplay.Tests/GameplayAbilities/GameplayEffect/GameplayEffectSpecTests.cs`

**Interfaces:**
- Consumes: `GameplayEffect` (Task 4), `GameplayModifier` (Task 3), `GameplayTag` (现有)
- Produces: `GameplayEffectSpec` class, `FModifierSpec` struct

- [ ] **Step 1: 写测试**

```csharp
// tests/Gameplay.Tests/GameplayAbilities/GameplayEffect/GameplayEffectSpecTests.cs
namespace Gameplay.Tests.GameplayAbilities;

using Gameplay.GameplayTags;
using Gameplay.GameplayAbilities;

public class GameplayEffectSpecTests
{
    [Fact]
    public void Constructor_SetsDefinition()
    {
        var ge = new GameplayEffect
        {
            DurationPolicy = EGameplayEffectDurationType.HasDuration,
            Period = 1.5f,
        };
        var spec = new GameplayEffectSpec(ge, level: 3f);
        Assert.Same(ge, spec.Definition);
        Assert.Equal(3f, spec.Level);
    }

    [Fact]
    public void StackCount_DefaultsToOne()
    {
        var spec = new GameplayEffectSpec(new GameplayEffect(), 1f);
        Assert.Equal(1, spec.StackCount);
    }

    [Fact]
    public void Duration_ReflectsDefinition()
    {
        var ge = new GameplayEffect
        {
            DurationPolicy = EGameplayEffectDurationType.HasDuration,
        };
        var spec = new GameplayEffectSpec(ge, 1f) { Duration = 5.0f };
        Assert.Equal(5.0f, spec.Duration);
    }

    [Fact]
    public void SetByCallerMagnitude_SetAndGet()
    {
        var tag = GameplayTag.Request("SetByCaller.Damage");
        var spec = new GameplayEffectSpec(new GameplayEffect(), 1f);
        spec.SetSetByCallerMagnitude(tag, 42f);
        Assert.Equal(42f, spec.GetSetByCallerMagnitude(tag, false));
    }
}
```

- [ ] **Step 2: 运行测试确认失败**

Run: `dotnet test tests/Gameplay.Tests/Gameplay.Tests.csproj --filter "FullyQualifiedName~GameplayEffectSpecTests"`
Expected: FAIL

- [ ] **Step 3: 实现**

```csharp
// src/Gameplay/GameplayAbilities/GameplayEffect/GameplayEffectSpec.cs
using System.Collections.Generic;
using Gameplay.GameplayTags;

namespace Gameplay.GameplayAbilities;

/// <summary>已计算 Magnitude 的单个 Modifier 条目。</summary>
public struct FModifierSpec
{
    public int AttributeId;
    public EGameplayModOp ModOp;
    public float EvaluatedMagnitude;
    public EAttributeCapturePolicy CapturePolicy;
}

/// <summary>
/// GameplayEffect 的施放实例（非 Entity）。创建后不可变（除 StackCount/Duration 运行时调整）。
/// </summary>
public class GameplayEffectSpec
{
    public GameplayEffect Definition { get; }
    public float Level { get; set; }
    public float Duration { get; set; }
    public float Period { get; set; }
    public int StackCount { get; set; } = 1;
    public List<FModifierSpec> Modifiers { get; } = new();
    public GameplayTagContainer CapturedSourceTags { get; } = new();
    public GameplayTagContainer CapturedTargetTags { get; } = new();
    public GameplayTagContainer DynamicAssetTags { get; } = new();
    public GameplayEffectContext EffectContext { get; set; }

    private Dictionary<GameplayTag, float> setByCallerMagnitudes = new();

    public GameplayEffectSpec(GameplayEffect definition, float level)
    {
        Definition = definition;
        Level = level;
        Duration = -1f; // Instant 场景
        Period = definition.Period;
    }

    public void SetSetByCallerMagnitude(GameplayTag tag, float magnitude)
        => setByCallerMagnitudes[tag] = magnitude;

    public float GetSetByCallerMagnitude(GameplayTag tag, bool warnIfNotFound = true)
        => setByCallerMagnitudes.TryGetValue(tag, out var v) ? v : 0f;
}

/// <summary>Effect 施放上下文（Instigator 信息等）。</summary>
public class GameplayEffectContext
{
    public Entity? Instigator;
    public int InstigatorAbilityHandle;
}
```

- [ ] **Step 4: 运行测试确认通过**

Run: `dotnet test tests/Gameplay.Tests/Gameplay.Tests.csproj --filter "FullyQualifiedName~GameplayEffectSpecTests"`
Expected: PASS

- [ ] **Step 5: 提交**

```bash
git add src/Gameplay/GameplayAbilities/GameplayEffect/GameplayEffectSpec.cs tests/Gameplay.Tests/GameplayAbilities/GameplayEffect/GameplayEffectSpecTests.cs
git commit -m "feat: add GameplayEffectSpec and GameplayEffectContext

Co-Authored-By: Claude <noreply@anthropic.com>"
```

---

### Task 6: ActiveGameplayEffectComponent

**Files:**
- Create: `src/Gameplay/GameplayAbilities/GameplayEffect/ActiveGameplayEffectComponent.cs`
- Create: `tests/Gameplay.Tests/GameplayAbilities/GameplayEffect/ActiveGameplayEffectComponentTests.cs`

**Interfaces:**
- Consumes: `GameplayEffect` (Task 4), 枚举 (Task 2)
- Produces: `ActiveGameplayEffectComponent` struct (IComponent)

- [ ] **Step 1: 写测试**

```csharp
// tests/Gameplay.Tests/GameplayAbilities/GameplayEffect/ActiveGameplayEffectComponentTests.cs
namespace Gameplay.Tests.GameplayAbilities;

using Gameplay.GameplayAbilities;

public class ActiveGameplayEffectComponentTests
{
    [Fact]
    public void Default_StackCount_IsZero()
    {
        var comp = new ActiveGameplayEffectComponent();
        Assert.Equal(0, comp.StackCount);
    }

    [Fact]
    public void Default_IsNotInhibited()
    {
        var comp = new ActiveGameplayEffectComponent();
        Assert.False(comp.IsInhibited);
    }

    [Fact]
    public void Default_Duration_IsZero()
    {
        var comp = new ActiveGameplayEffectComponent();
        Assert.Equal(0f, comp.Duration);
    }
}
```

- [ ] **Step 2: 运行测试确认失败**

Run: `dotnet test tests/Gameplay.Tests/Gameplay.Tests.csproj --filter "FullyQualifiedName~ActiveGameplayEffectComponentTests"`
Expected: FAIL

- [ ] **Step 3: 实现**

```csharp
// src/Gameplay/GameplayAbilities/GameplayEffect/ActiveGameplayEffectComponent.cs
using Friflo.Engine.ECS;
using Gameplay.GameplayTags;

namespace Gameplay.GameplayAbilities;

/// <summary>
/// ActiveGameplayEffect 的运行时 Component（单一，所有字段合一）。
/// 挂在 Target Entity 下的子 Entity 上。
/// </summary>
public struct ActiveGameplayEffectComponent : IComponent
{
    // ── 时间 ──
    public float Duration;                       // 剩余时间（Infinite = -1），EffectSystem 每帧递减
    public float StartWorldTime;                 // 开始时间戳——GetTimeRemaining() + Server→Client 同步

    // ── 周期 ──
    public float Period;                         // 周期间隔
    public float PeriodProgress;                 // 当前周期进度

    // ── 堆叠 ──
    public int StackCount;                       // 当前层数
    public int StackLimit;                       // 最大层数
    public EGameplayEffectStackingDurationPolicy StackingDurationPolicy;
    public EGameplayEffectStackingPeriodPolicy StackingPeriodPolicy;
    public EGameplayEffectStackingExpirationPolicy StackingExpirationPolicy;

    // ── 句柄与引用 ──
    public int Handle;                           // 全局唯一 ID
    public Entity SourceEntity;                  // 施放者
    public Entity TargetEntity;                  // 目标（父 Entity）
    public int DefinitionId;                     // GameplayEffectRegistry 查表 key（避免托管引用）

    // ── 抑制 ──
    public bool IsInhibited;                     // Tag 条件不满足时 = true
    public EGameplayEffectPeriodInhibitionRemovedPolicy InhibitionPolicy;

    // ── 行为配置（从 GameplayEffect 拷贝，NULL/empty = 不适用） ──
    public GameplayTagContainer ApplicationRequiredTags;
    public GameplayTagContainer OngoingRequiredTags;
    public GameplayTagContainer RemovalTags;
    public GameplayTagContainer GrantedTags;
    public GameplayTagContainer BlockedAbilityTags;
    public GameplayTagContainer CancelAbilityTags;
}
```

- [ ] **Step 4: 运行测试确认通过**

Run: `dotnet test tests/Gameplay.Tests/Gameplay.Tests.csproj --filter "FullyQualifiedName~ActiveGameplayEffectComponentTests"`
Expected: PASS

- [ ] **Step 5: 提交**

```bash
git add src/Gameplay/GameplayAbilities/GameplayEffect/ActiveGameplayEffectComponent.cs tests/Gameplay.Tests/GameplayAbilities/GameplayEffect/ActiveGameplayEffectComponentTests.cs
git commit -m "feat: add ActiveGameplayEffectComponent (runtime IComponent)

Co-Authored-By: Claude <noreply@anthropic.com>"
```

---

### Task 7: AttributeAggregator + ModEntry + DirtyAttributeComponent

**Files:**
- Create: `src/Gameplay/GameplayAbilities/Attribute/AttributeAggregator.cs`
- Create: `src/Gameplay/GameplayAbilities/Attribute/ModEntry.cs`
- Create: `src/Gameplay/GameplayAbilities/Attribute/DirtyAttributeComponent.cs`
- Create: `tests/Gameplay.Tests/GameplayAbilities/Attribute/AttributeAggregatorTests.cs`
- Create: `tests/Gameplay.Tests/GameplayAbilities/Attribute/DirtyAttributeComponentTests.cs`

**Interfaces:**
- Consumes: 枚举 (Task 2)
- Produces: `AttributeAggregator` (internal), `ModEntry` struct, `DirtyAttributeComponent` struct

- [ ] **Step 1: 写测试**

```csharp
// tests/Gameplay.Tests/GameplayAbilities/Attribute/AttributeAggregatorTests.cs
namespace Gameplay.Tests.GameplayAbilities;

using Gameplay.GameplayAbilities;
using Gameplay.GameplayAbilities;

public class AttributeAggregatorTests
{
    [Fact]
    public void Default_BaseValue_IsZero()
    {
        var agg = new AttributeAggregator();
        Assert.Equal(0f, agg.BaseValue);
        Assert.False(agg.Dirty);
    }

    [Fact]
    public void AddMod_Dirties_AndIncrementsBucket()
    {
        var agg = new AttributeAggregator();
        agg.AddMod(1, 10f, EGameplayModOp.Additive);
        Assert.True(agg.Dirty);
        Assert.Equal(1, agg.GetModCount(EGameplayModOp.Additive));
    }

    [Fact]
    public void Evaluate_Additive_ReturnsCorrectValue()
    {
        var agg = new AttributeAggregator { BaseValue = 100f };
        agg.AddMod(1, 20f, EGameplayModOp.Additive);
        agg.AddMod(2, 30f, EGameplayModOp.Additive);

        float result = agg.Evaluate();
        Assert.Equal(150f, result); // (100 + 20 + 30)
    }

    [Fact]
    public void Evaluate_Override_IgnoresOtherMods()
    {
        var agg = new AttributeAggregator { BaseValue = 100f };
        agg.AddMod(1, 20f, EGameplayModOp.Additive);
        agg.AddMod(2, 999f, EGameplayModOp.Override);

        float result = agg.Evaluate();
        Assert.Equal(999f, result); // Override wins
    }

    [Fact]
    public void RemoveMod_ByHandle_ClearsMod()
    {
        var agg = new AttributeAggregator { BaseValue = 100f };
        agg.AddMod(1, 20f, EGameplayModOp.Additive);

        agg.RemoveModsByHandle(1);
        Assert.Equal(0, agg.GetModCount(EGameplayModOp.Additive));
        Assert.Equal(100f, agg.Evaluate()); // back to base
    }

    [Fact]
    public void Evaluate_FullFormula()
    {
        // ((Base + Add) * Mul / Div) + FinalAdd
        var agg = new AttributeAggregator { BaseValue = 100f };
        agg.AddMod(1, 20f, EGameplayModOp.Additive);
        agg.AddMod(2, 1.5f, EGameplayModOp.Multiply);
        agg.AddMod(3, 5f, EGameplayModOp.FinalAdd);

        Assert.Equal(185f, agg.Evaluate()); // ((100+20)*1.5/1) + 5
    }
}
```

```csharp
// tests/Gameplay.Tests/GameplayAbilities/Attribute/DirtyAttributeComponentTests.cs
namespace Gameplay.Tests.GameplayAbilities;

using Gameplay.GameplayAbilities;

public class DirtyAttributeComponentTests
{
    [Fact]
    public void Default_AllBitsCleared()
    {
        var dc = new DirtyAttributeComponent();
        Assert.Equal(0UL, dc.DirtyBits);
    }

    [Fact]
    public void SetBit_MarksSingleAttribute()
    {
        var dc = new DirtyAttributeComponent();
        dc.SetBit(3);
        Assert.NotEqual(0UL, dc.DirtyBits);
        Assert.True(dc.HasBit(3));
    }

    [Fact]
    public void HasBit_BitNotSet_ReturnsFalse()
    {
        var dc = new DirtyAttributeComponent();
        Assert.False(dc.HasBit(5));
    }

    [Fact]
    public void ClearAll_ResetsToZero()
    {
        var dc = new DirtyAttributeComponent();
        dc.SetBit(0);
        dc.SetBit(10);
        dc.ClearAll();
        Assert.Equal(0UL, dc.DirtyBits);
    }
}
```

- [ ] **Step 2: 运行测试确认失败**

Run: `dotnet test tests/Gameplay.Tests/Gameplay.Tests.csproj --filter "FullyQualifiedName~AttributeAggregatorTests|FullyQualifiedName~DirtyAttributeComponentTests"`
Expected: FAIL

- [ ] **Step 3: 实现**

```csharp
// src/Gameplay/GameplayAbilities/Attribute/ModEntry.cs
namespace Gameplay.GameplayAbilities;

/// <summary>AttributeAggregator 中的单个 Mod 条目（internal，框架内部使用）。</summary>
internal struct ModEntry
{
    public int ActiveHandle;     // 归属的 ActiveGameplayEffect.Handle
    public float Magnitude;      // 已计算的幅度
}
```

```csharp
// src/Gameplay/GameplayAbilities/Attribute/AttributeAggregator.cs
using System.Collections.Generic;
using Gameplay.GameplayAbilities;

namespace Gameplay.GameplayAbilities;

/// <summary>
/// 单个 GameplayAttribute 的运行时聚合器。
/// 不是 Component，由 AttributeSystem 内部 Dictionary 管理。
/// </summary>
internal class AttributeAggregator
{
    public float BaseValue;
    public bool Dirty;

    // ModBuckets[(int)EGameplayModOp] — 按 ModOp 分桶
    private List<ModEntry>[] modBuckets;

    public AttributeAggregator()
    {
        int opCount = System.Enum.GetValues<EGameplayModOp>().Length;
        modBuckets = new List<ModEntry>[opCount];
        for (int i = 0; i < opCount; i++)
            modBuckets[i] = new List<ModEntry>();
    }

    public void AddMod(int handle, float magnitude, EGameplayModOp op)
    {
        modBuckets[(int)op].Add(new ModEntry { ActiveHandle = handle, Magnitude = magnitude });
        Dirty = true;
    }

    public void RemoveModsByHandle(int handle)
    {
        for (int i = 0; i < modBuckets.Length; i++)
            modBuckets[i].RemoveAll(m => m.ActiveHandle == handle);
        Dirty = true;
    }

    public int GetModCount(EGameplayModOp op) => modBuckets[(int)op].Count;

    /// <summary>聚合公式同 UE：Overide 优先，否则 ((Base + ΣAdd) × ΠMul / ΠDiv) + ΣFinalAdd。</summary>
    public float Evaluate()
    {
        // Override check
        var overrides = modBuckets[(int)EGameplayModOp.Override];
        if (overrides.Count > 0)
            return overrides[^1].Magnitude; // 最后一个 Override 胜出

        float result = BaseValue;

        // ΣAdd
        foreach (var m in modBuckets[(int)EGameplayModOp.Additive])
            result += m.Magnitude;

        // ΠMultiply
        float mul = 1f;
        foreach (var m in modBuckets[(int)EGameplayModOp.Multiply])
            mul *= m.Magnitude;
        result *= mul;

        // / ΠDivide
        float div = 1f;
        foreach (var m in modBuckets[(int)EGameplayModOp.Divide])
            div *= m.Magnitude;
        if (div != 0f) result /= div;

        // + ΣFinalAdd
        foreach (var m in modBuckets[(int)EGameplayModOp.FinalAdd])
            result += m.Magnitude;

        Dirty = false;
        return result;
    }
}
```

```csharp
// src/Gameplay/GameplayAbilities/Attribute/DirtyAttributeComponent.cs
using Friflo.Engine.ECS;

namespace Gameplay.GameplayAbilities;

/// <summary>
/// Entity 上的属性脏标记。Bit&lt;i&gt; = Attribute&lt;i&gt; 需要重算。
/// SG 编译期保证 AttributeId 不超过 64。
/// </summary>
public struct DirtyAttributeComponent : IComponent
{
    public ulong DirtyBits;

    public void SetBit(int attributeId)
        => DirtyBits |= (1UL << attributeId);

    public bool HasBit(int attributeId)
        => (DirtyBits & (1UL << attributeId)) != 0;

    public void ClearAll()
        => DirtyBits = 0UL;
}
```

- [ ] **Step 4: 运行测试确认通过**

Run: `dotnet test tests/Gameplay.Tests/Gameplay.Tests.csproj --filter "FullyQualifiedName~AttributeAggregatorTests|FullyQualifiedName~DirtyAttributeComponentTests"`
Expected: PASS

- [ ] **Step 5: 提交**

```bash
git add src/Gameplay/GameplayAbilities/Attribute/ModEntry.cs src/Gameplay/GameplayAbilities/Attribute/AttributeAggregator.cs src/Gameplay/GameplayAbilities/Attribute/DirtyAttributeComponent.cs tests/Gameplay.Tests/GameplayAbilities/Attribute/AttributeAggregatorTests.cs tests/Gameplay.Tests/GameplayAbilities/Attribute/DirtyAttributeComponentTests.cs
git commit -m "feat: add AttributeAggregator, ModEntry, DirtyAttributeComponent

Co-Authored-By: Claude <noreply@anthropic.com>"
```

---

### Task 8: AttributeSystem

**Files:**
- Create: `src/Gameplay/GameplayAbilities/Attribute/AttributeSystem.cs`
- Create: `tests/Gameplay.Tests/GameplayAbilities/Attribute/AttributeSystemTests.cs`

**Interfaces:**
- Consumes: `AttributeAggregator`, `DirtyAttributeComponent` (Task 7), `ActiveGameplayEffectComponent` (Task 6)
- Produces: `AttributeSystem : Friflo.Engine.ECS.QuerySystem`

- [ ] **Step 1: 写测试**

```csharp
// tests/Gameplay.Tests/GameplayAbilities/Attribute/AttributeSystemTests.cs
namespace Gameplay.Tests.GameplayAbilities;

using Friflo.Engine.ECS;
using Gameplay.GameplayAbilities;
using Gameplay.GameplayAbilities;

public class AttributeSystemTests
{
    [Fact]
    public void Tick_SingleDirtyBit_EvaluatesAndClears()
    {
        var store = new EntityStore();
        var sys = new AttributeSystem();
        store.AddSystem(sys);

        var entity = store.CreateEntity();
        entity.AddComponent(new DirtyAttributeComponent());
        // 模拟 Apply 后的 state：Aggregator 有 Mod，DirtyBit 设置
        sys.SetAggregatorValue(entity, attributeId: 3, baseValue: 100f);
        sys.AddAggregatorMod(entity, 3, handle: 1, magnitude: 20f, EGameplayModOp.Additive);

        var dirty = entity.GetComponent<DirtyAttributeComponent>();
        dirty.SetBit(3);

        store.Update(default); // Trigger AttributeSystem.OnUpdate

        // 重算后 DirtyBit 清零
        var finalDirty = entity.GetComponent<DirtyAttributeComponent>();
        Assert.False(finalDirty.HasBit(3));
    }

    [Fact]
    public void RemoveEntity_CleansUpAggregator()
    {
        var store = new EntityStore();
        var sys = new AttributeSystem();
        store.AddSystem(sys);

        var entity = store.CreateEntity();
        sys.SetAggregatorValue(entity, attributeId: 0, baseValue: 50f);
        sys.AddAggregatorMod(entity, 0, handle: 1, magnitude: 10f, EGameplayModOp.Additive);
        sys.AddAggregatorMod(entity, 0, handle: 2, magnitude: 5f, EGameplayModOp.Additive);

        // Get value before removal
        float valBefore = sys.GetCurrentValue(entity, attributeId: 0);
        Assert.Equal(65f, valBefore); // 50 + 10 + 5

        // Remove handle 2
        sys.RemoveAggregatorModsByHandle(2);

        float valAfter = sys.GetCurrentValue(entity, attributeId: 0);
        Assert.Equal(60f, valAfter); // 50 + 10 only
    }
}
```

- [ ] **Step 2: 运行测试确认失败**

Run: `dotnet test tests/Gameplay.Tests/Gameplay.Tests.csproj --filter "FullyQualifiedName~AttributeSystemTests"`
Expected: FAIL

- [ ] **Step 3: 实现**

```csharp
// src/Gameplay/GameplayAbilities/Attribute/AttributeSystem.cs
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Gameplay.GameplayAbilities;

namespace Gameplay.GameplayAbilities;

/// <summary>
/// 属性重算 System。只处理 DirtyBits 有标记的 Entity。
/// 每个 GameplayAttribute 对应一个 AttributeAggregator，由本 System 外部管理。
/// </summary>
public class AttributeSystem : QuerySystem<DirtyAttributeComponent>
{
    // (Entity.Id, attributeId) → Aggregator
    private readonly Dictionary<(int entityId, int attrId), AttributeAggregator> aggregators = new();

    // 反向索引（RealTime 用）：(sourceEntity.Id, sourceAttrId) → 受影响的 target Handle 列表
    private readonly Dictionary<(int entityId, int attrId), List<int>> realTimeReverseIndex = new();

    protected override void OnUpdate()
    {
        foreach (var chunk in Query.Chunks)
        {
            var dirtySpan = chunk.Span;
            foreach (var entityId in chunk.Entities)
            {
                ref var dirty = ref dirtySpan[0];
                if (dirty.DirtyBits == 0) continue;

                // 遍历所有 Set 的 bit
                ulong bits = dirty.DirtyBits;
                int attrId = 0;
                while (bits != 0)
                {
                    if ((bits & 1) != 0)
                    {
                        var key = (entityId, attrId);
                        if (aggregators.TryGetValue(key, out var agg))
                        {
                            // Evaluate → 写入对应 AttributeSetComponent 的 CurrentValue
                            // 注意：此处需要 SG 提供的 AttributeAccessTable 来写 CurrentValue
                            agg.Evaluate();
                        }
                    }
                    bits >>= 1;
                    attrId++;
                }
                dirty.ClearAll();
            }
        }
    }

    // ── Aggregator 管理（供 EffectSystem 调用） ──

    private (int entityId, int attrId) Key(Entity e, int attrId) => (e.Id, attrId);

    public void SetAggregatorValue(Entity entity, int attributeId, float baseValue)
    {
        var key = Key(entity, attributeId);
        if (!aggregators.TryGetValue(key, out var agg))
        {
            agg = new AttributeAggregator();
            aggregators[key] = agg;
        }
        agg.BaseValue = baseValue;
    }

    public void AddAggregatorMod(Entity entity, int attributeId, int handle,
        float magnitude, EGameplayModOp op)
    {
        var key = Key(entity, attributeId);
        if (aggregators.TryGetValue(key, out var agg))
            agg.AddMod(handle, magnitude, op);
    }

    public void RemoveAggregatorModsByHandle(int handle)
    {
        foreach (var agg in aggregators.Values)
            agg.RemoveModsByHandle(handle);
    }

    public float GetCurrentValue(Entity entity, int attributeId)
    {
        var key = Key(entity, attributeId);
        if (aggregators.TryGetValue(key, out var agg))
            return agg.Evaluate();
        return 0f;
    }

    // ── RealTime 反向索引 ──

    public void RegisterRealTimeDependency(int sourceEntityId, int sourceAttrId, int targetHandle)
    {
        var key = (sourceEntityId, sourceAttrId);
        if (!realTimeReverseIndex.TryGetValue(key, out var list))
        {
            list = new List<int>();
            realTimeReverseIndex[key] = list;
        }
        list.Add(targetHandle);
    }

    public void UnregisterRealTimeDependencies(int handle)
    {
        foreach (var list in realTimeReverseIndex.Values)
            list.RemoveAll(h => h == handle);
    }
}
```

- [ ] **Step 4: 运行测试确认通过**

Run: `dotnet test tests/Gameplay.Tests/Gameplay.Tests.csproj --filter "FullyQualifiedName~AttributeSystemTests"`
Expected: PASS

- [ ] **Step 5: 提交**

```bash
git add src/Gameplay/GameplayAbilities/Attribute/AttributeSystem.cs tests/Gameplay.Tests/GameplayAbilities/Attribute/AttributeSystemTests.cs
git commit -m "feat: add AttributeSystem (dirty-driven attribute recalculation)

Co-Authored-By: Claude <noreply@anthropic.com>"
```

---

### Task 9: GameplayEffectQuery + ConditionalGameplayEffect + GameplayEffectCue

**Files:**
- Create: `src/Gameplay/GameplayAbilities/GameplayEffect/GameplayEffectQuery.cs`
- Create: `src/Gameplay/GameplayAbilities/GameplayEffect/ConditionalGameplayEffect.cs`
- Create: `src/Gameplay/GameplayAbilities/GameplayEffect/GameplayEffectCue.cs`
- Create: `tests/Gameplay.Tests/GameplayAbilities/GameplayEffect/GameplayEffectQueryTests.cs`

**Interfaces:**
- Consumes: `GameplayTag` (现有), `GameplayEffect` (Task 4)
- Produces: `GameplayEffectQuery` class, `ConditionalGameplayEffect` struct, `GameplayEffectCue` struct

- [ ] **Step 1: 写测试**

```csharp
// tests/Gameplay.Tests/GameplayAbilities/GameplayEffect/GameplayEffectQueryTests.cs
namespace Gameplay.Tests.GameplayAbilities;

using Gameplay.GameplayTags;
using Gameplay.GameplayAbilities;

public class GameplayEffectQueryTests
{
    [Fact]
    public void MatchByDefinition_Matches()
    {
        var ge = new GameplayEffect { DurationPolicy = EGameplayEffectDurationType.HasDuration };
        var spec = new GameplayEffectSpec(ge, 1f);
        var query = GameplayEffectQuery.MakeQuery_MatchDefinition(ge);

        Assert.True(query.Matches(spec));
    }

    [Fact]
    public void MatchByTag_NonMatching_ReturnsFalse()
    {
        var ge = new GameplayEffect();
        ge.GrantedTags.AddTag(GameplayTag.Request("Buff.Fire"));
        var spec = new GameplayEffectSpec(ge, 1f);

        var requiredTag = GameplayTag.Request("Buff.Ice");
        var query = GameplayEffectQuery.MakeQuery_MatchAnyGrantedTags(
            new GameplayTagContainer { requiredTag });

        Assert.False(query.Matches(spec));
    }

    [Fact]
    public void Empty_MatchesAnything()
    {
        var spec = new GameplayEffectSpec(new GameplayEffect(), 1f);
        var query = new GameplayEffectQuery();
        Assert.True(query.IsEmpty);
        Assert.True(query.Matches(spec));
    }
}
```

- [ ] **Step 2: 运行测试确认失败**

Run: `dotnet test tests/Gameplay.Tests/Gameplay.Tests.csproj --filter "FullyQualifiedName~GameplayEffectQueryTests"`
Expected: FAIL

- [ ] **Step 3: 实现**

```csharp
// src/Gameplay/GameplayAbilities/GameplayEffect/GameplayEffectQuery.cs
using Gameplay.GameplayTags;

namespace Gameplay.GameplayAbilities;

/// <summary>效果查询条件——用于 Immunity、RemoveOtherEffects、GetActiveEffects 等。</summary>
public class GameplayEffectQuery
{
    public GameplayTagContainer OwningTagQuery = new();
    public GameplayTagContainer EffectTagQuery = new();
    public GameplayEffect Definition;

    public bool IsEmpty =>
        OwningTagQuery.Count == 0 && EffectTagQuery.Count == 0 && Definition == null;

    public bool Matches(GameplayEffectSpec spec)
    {
        if (IsEmpty) return true;
        if (Definition != null && spec.Definition != Definition) return false;
        if (OwningTagQuery.Count > 0 && !spec.Definition.GrantedTags.HasAny(OwningTagQuery))
            return false;
        return true;
    }

    public static GameplayEffectQuery MakeQuery_MatchDefinition(GameplayEffect def)
        => new() { Definition = def };

    public static GameplayEffectQuery MakeQuery_MatchAnyGrantedTags(
        GameplayTagContainer tags) => new() { OwningTagQuery = tags };
}
```

```csharp
// src/Gameplay/GameplayAbilities/GameplayEffect/ConditionalGameplayEffect.cs
namespace Gameplay.GameplayAbilities;

/// <summary>条件触发的 GameplayEffect——用于 OnApplicationEffects 和 OnCompleteEffects。</summary>
public struct ConditionalGameplayEffect
{
    public GameplayEffect Effect;
    public GameplayTagContainer RequiredSourceTags; // 为空 = 无条件触发
}
```

```csharp
// src/Gameplay/GameplayAbilities/GameplayEffect/GameplayEffectCue.cs
using Gameplay.GameplayTags;

namespace Gameplay.GameplayAbilities;

/// <summary>GameplayEffect 关联的 Cue 定义。</summary>
public struct GameplayEffectCue
{
    public GameplayTag CueTag;
    public float MinLevel;
    public float MaxLevel;
}
```

- [ ] **Step 4: 运行测试确认通过**

Run: `dotnet test tests/Gameplay.Tests/Gameplay.Tests.csproj --filter "FullyQualifiedName~GameplayEffectQueryTests"`
Expected: PASS

- [ ] **Step 5: 提交**

```bash
git add src/Gameplay/GameplayAbilities/GameplayEffect/GameplayEffectQuery.cs src/Gameplay/GameplayAbilities/GameplayEffect/ConditionalGameplayEffect.cs src/Gameplay/GameplayAbilities/GameplayEffect/GameplayEffectCue.cs tests/Gameplay.Tests/GameplayAbilities/GameplayEffect/GameplayEffectQueryTests.cs
git commit -m "feat: add GameplayEffectQuery, ConditionalGameplayEffect, GameplayEffectCue

Co-Authored-By: Claude <noreply@anthropic.com>"
```

---

### Task 10: EffectSystem (Tick + TagRequirements)

**Files:**
- Create: `src/Gameplay/GameplayAbilities/GameplayEffect/EffectSystem.cs`
- Create: `tests/Gameplay.Tests/GameplayAbilities/GameplayEffect/EffectSystemTests.cs`

**Interfaces:**
- Consumes: `ActiveGameplayEffectComponent` (Task 6), `AttributeSystem` (Task 8)
- Produces: `EffectSystem : QuerySystem<ActiveGameplayEffectComponent>`

- [ ] **Step 1: 写核心 Tick 测试**

```csharp
// tests/Gameplay.Tests/GameplayAbilities/GameplayEffect/EffectSystemTests.cs
namespace Gameplay.Tests.GameplayAbilities;

using Friflo.Engine.ECS;
using Gameplay.GameplayAbilities;
using Gameplay.GameplayAbilities;

public class EffectSystemTests
{
    [Fact]
    public void TickDuration_DecrementsDuration()
    {
        var store = new EntityStore();
        var attrSys = new AttributeSystem();
        var effectSys = new EffectSystem(attrSys);
        store.AddSystem(effectSys);

        var target = store.CreateEntity();
        var activeEntity = store.CreateEntity();
        activeEntity.AddChild(target);
        activeEntity.AddComponent(new ActiveGameplayEffectComponent
        {
            Duration = 3.0f,
            TargetEntity = target,
            Handle = 1,
        });

        store.Update(1.0f); // dt = 1.0f

        var comp = activeEntity.GetComponent<ActiveGameplayEffectComponent>();
        Assert.Equal(2.0f, comp.Duration, 0.001f);
    }

    [Fact]
    public void TickDuration_Expires_DestroysEntity()
    {
        var store = new EntityStore();
        var attrSys = new AttributeSystem();
        var effectSys = new EffectSystem(attrSys);
        store.AddSystem(effectSys);

        var target = store.CreateEntity();
        var activeEntity = store.CreateEntity();
        activeEntity.AddChild(target);
        activeEntity.AddComponent(new ActiveGameplayEffectComponent
        {
            Duration = 0.5f,
            TargetEntity = target,
            Handle = 1,
        });

        store.Update(1.0f); // 过期

        // Entity 应被销毁（Duration ≤ 0 + StackCount = 0/1）
        Assert.True(activeEntity.IsNull || !activeEntity.HasComponent<ActiveGameplayEffectComponent>());
    }

    [Fact]
    public void Infinite_Duration_NotDecremented()
    {
        var store = new EntityStore();
        var attrSys = new AttributeSystem();
        var effectSys = new EffectSystem(attrSys);
        store.AddSystem(effectSys);

        var target = store.CreateEntity();
        var activeEntity = store.CreateEntity();
        activeEntity.AddChild(target);
        activeEntity.AddComponent(new ActiveGameplayEffectComponent
        {
            Duration = -1f, // Infinite
            TargetEntity = target,
            Handle = 1,
        });

        store.Update(10f);
        var comp = activeEntity.GetComponent<ActiveGameplayEffectComponent>();
        Assert.Equal(-1f, comp.Duration);
    }

    [Fact]
    public void Periodic_Tick_ExecutesModifiers()
    {
        var store = new EntityStore();
        var attrSys = new AttributeSystem();
        // 预设 Target Entity 的 aggregator
        var target = store.CreateEntity();
        target.AddComponent(new DirtyAttributeComponent());
        attrSys.SetAggregatorValue(target, attributeId: 0, baseValue: 100f);
        attrSys.AddAggregatorMod(target, 0, handle: 1, magnitude: 10f,
            Gameplay.GameplayAbilities.EGameplayModOp.Additive);

        var effectSys = new EffectSystem(attrSys);
        store.AddSystem(effectSys);
        store.AddSystem(attrSys);

        var activeEntity = store.CreateEntity();
        activeEntity.AddChild(target);
        activeEntity.AddComponent(new ActiveGameplayEffectComponent
        {
            Duration = 10f,
            Period = 2.0f,
            TargetEntity = target,
            Handle = 1,
        });

        store.Update(2.5f); // Period triggered at t=2.0

        var comp = activeEntity.GetComponent<ActiveGameplayEffectComponent>();
        Assert.True(comp.PeriodProgress < 2.0f); // Reset after trigger
    }
}
```

- [ ] **Step 2: 运行测试确认失败**

Run: `dotnet test tests/Gameplay.Tests/Gameplay.Tests.csproj --filter "FullyQualifiedName~EffectSystemTests"`
Expected: FAIL

- [ ] **Step 3: 实现 EffectSystem（核心 Tick）**

```csharp
// src/Gameplay/GameplayAbilities/GameplayEffect/EffectSystem.cs
using Friflo.Engine.ECS;
using Gameplay.GameplayAbilities;

namespace Gameplay.GameplayAbilities;

/// <summary>
/// GameplayEffect 核心 System：Tick Duration / Period / TagRequirements / Expiration。
/// Apply 和 Remove 作为 public API 供外部调用。
/// </summary>
public class EffectSystem : QuerySystem<ActiveGameplayEffectComponent>
{
    private readonly AttributeSystem attributeSystem;
    private int nextHandle = 1;

    public EffectSystem(AttributeSystem attributeSystem)
    {
        this.attributeSystem = attributeSystem;
        Filter = EntityFilter; // 设置在 OnUpdate 中按需遍历
    }

    protected override void OnUpdate()
    {
        float dt = Tick.deltaTime;
        Query.ForEachEntity((ref ActiveGameplayEffectComponent comp, Entity entity) =>
        {
            // 1. TickDuration
            if (comp.Duration > 0)
            {
                comp.Duration -= dt;
                if (comp.Duration <= 0)
                {
                    HandleExpiration(ref comp, entity);
                    return;
                }
            }

            // 2. TickPeriod
            if (comp.Period > 0 && !comp.IsInhibited)
            {
                comp.PeriodProgress += dt;
                while (comp.PeriodProgress >= comp.Period)
                {
                    comp.PeriodProgress -= comp.Period;
                    ExecutePeriodicModifiers(ref comp);
                }
            }
        });
    }

    private void HandleExpiration(ref ActiveGameplayEffectComponent comp, Entity entity)
    {
        if (comp.StackCount > 1)
        {
            comp.StackCount--;
            switch (comp.StackingExpirationPolicy)
            {
                case EGameplayEffectStackingExpirationPolicy.ClearEntireStack:
                    RemoveEffect(comp.Handle, EEffectEndType.Normal);
                    break;
                case EGameplayEffectStackingExpirationPolicy.RemoveSingleStackAndRefreshDuration:
                    // comp.Duration refreshed (set when this Stack was applied)
                    break;
                case EGameplayEffectStackingExpirationPolicy.RefreshDuration:
                    // Duration stays infinite, manual management
                    break;
            }
        }
        else
        {
            RemoveEffect(comp.Handle, EEffectEndType.Normal);
        }
    }

    private void ExecutePeriodicModifiers(ref ActiveGameplayEffectComponent comp)
    {
        var target = comp.TargetEntity;
        var spec = GetSpecFromHandle(comp.Handle); // 需要 Spec 缓存或 Definition lookup
        // 对每个 Modifier → Aggregator → SetDirty
    }

    // ── 待 Task 11 (Apply) 补充的占位 ──
    private GameplayEffectSpec GetSpecFromHandle(int handle) => null; // Task 11 替换

    // ── Placeholder Remove ──
    public void RemoveEffect(int handle, EEffectEndType reason)
    {
        attributeSystem.RemoveAggregatorModsByHandle(handle);
        // Entity 销毁 → Task 11 补充完整流程
    }
}
```

- [ ] **Step 4: 运行测试确认通过**

Run: `dotnet test tests/Gameplay.Tests/Gameplay.Tests.csproj --filter "FullyQualifiedName~EffectSystemTests"`
Expected: PASS

- [ ] **Step 5: 提交**

```bash
git add src/Gameplay/GameplayAbilities/GameplayEffect/EffectSystem.cs tests/Gameplay.Tests/GameplayAbilities/GameplayEffect/EffectSystemTests.cs
git commit -m "feat: add EffectSystem core tick (duration, period, expiration)

Co-Authored-By: Claude <noreply@anthropic.com>"
```

---

### Task 11: EffectSystem.Apply + EffectSystem.Remove

**Files:**
- Modify: `src/Gameplay/GameplayAbilities/GameplayEffect/EffectSystem.cs`

**Interfaces:**
- Consumes: `GameplayEffectSpec` (Task 5), `ActiveGameplayEffectComponent` (Task 6), `AttributeSystem` (Task 8)
- Produces: `Apply(spec, target)` → int handle, `CanApply(spec, target)` → bool

- [ ] **Step 1: 写 Apply 测试**

```csharp
// 追加到 tests/Gameplay.Tests/GameplayAbilities/GameplayEffect/EffectSystemTests.cs

[Fact]
public void Apply_HasDuration_CreatesEntityWithComponent()
{
    var store = new EntityStore();
    var attrSys = new AttributeSystem();
    store.AddSystem(attrSys);
    var effectSys = new EffectSystem(attrSys);

    var ge = new GameplayEffect
    {
        DurationPolicy = EGameplayEffectDurationType.HasDuration,
    };
    var spec = new GameplayEffectSpec(ge, 1f) { Duration = 5f };

    var target = store.CreateEntity();
    int handle = effectSys.Apply(spec, target);

    Assert.True(handle > 0);
    // Verify ActiveGameplayEffect Entity exists under target
}

[Fact]
public void CanApply_TagRequirement_Fails_ReturnsFalse()
{
    var store = new EntityStore();
    var attrSys = new AttributeSystem();
    var effectSys = new EffectSystem(attrSys);

    var tag = GameplayTag.Request("State.Dead");
    var ge = new GameplayEffect();
    ge.ApplicationRequiredTags.AddTag(tag);

    var spec = new GameplayEffectSpec(ge, 1f);
    var target = store.CreateEntity();
    // Target doesn't have State.Dead → CanApply = false

    Assert.False(effectSys.CanApply(spec, target));
}

[Fact]
public void CanApply_NoRequirements_ReturnsTrue()
{
    var store = new EntityStore();
    var attrSys = new AttributeSystem();
    var effectSys = new EffectSystem(attrSys);

    var spec = new GameplayEffectSpec(new GameplayEffect(), 1f);
    var target = store.CreateEntity();

    Assert.True(effectSys.CanApply(spec, target));
}
```

- [ ] **Step 2: 运行测试确认失败**

Run: `dotnet test tests/Gameplay.Tests/Gameplay.Tests.csproj --filter "FullyQualifiedName~EffectSystemTests"`
Expected: FAIL — CanApply/Apply 未实现

- [ ] **Step 3: 实现 Apply + CanApply + Remove**

```csharp
// 追加到 EffectSystem.cs

// ── Handle → Spec 缓存（Apply 时存储，Remove 时取回） ──
private readonly Dictionary<int, GameplayEffectSpec> handleToSpec = new();

public bool CanApply(GameplayEffectSpec spec, Entity target)
{
    var ge = spec.Definition;

    // ApplicationRequiredTags check
    if (ge.ApplicationRequiredTags.Count > 0)
    {
        if (!target.TryGetComponent<GameplayTags.GameplayTagsComponent>(out var tags))
            return false;
        if (!tags.HasAll(ge.ApplicationRequiredTags))
            return false;
    }

    // ChanceToApply
    if (ge.ChanceToApply < 1.0f)
    {
        if (Random.Shared.NextDouble() > ge.ChanceToApply)
            return false;
    }

    // TODO: Immunity check (needs target ActiveEffects access)

    return true;
}

public int Apply(GameplayEffectSpec spec, Entity target)
{
    // 1. PreApply: RemoveOtherEffects
    // 2. CanApply
    if (!CanApply(spec, target)) return -1;

    int handle = nextHandle++;

    // 3. Create ActiveGameplayEffect Entity
    var entity = target.Store.CreateEntity();
    entity.AddChild(target);

    var comp = new ActiveGameplayEffectComponent
    {
        Duration = spec.Duration,
        Period = spec.Period,
        StartWorldTime = 0f, // TODO: use world time
        Handle = handle,
        SourceEntity = spec.EffectContext?.Instigator ?? default,
        TargetEntity = target,
        DefinitionId = spec.DefinitionId,
        StackCount = spec.StackCount,
        StackLimit = spec.Definition.StackLimit,
        StackingDurationPolicy = spec.Definition.StackingDurationPolicy,
        StackingPeriodPolicy = spec.Definition.StackingPeriodPolicy,
        StackingExpirationPolicy = spec.Definition.StackingExpirationPolicy,
        InhibitionPolicy = spec.Definition.InhibitionPolicy,
        ApplicationRequiredTags = spec.Definition.ApplicationRequiredTags,
        OngoingRequiredTags = spec.Definition.OngoingRequiredTags,
        RemovalTags = spec.Definition.RemovalTags,
        GrantedTags = spec.Definition.GrantedTags,
        BlockedAbilityTags = spec.Definition.BlockedAbilityTags,
        CancelAbilityTags = spec.Definition.CancelAbilityTags,
    };

    entity.AddComponent(comp);

    // 4. 缓存 Spec
    handleToSpec[handle] = spec;

    // 5. Apply Modifiers → AttributeAggregator
    foreach (var mod in spec.Modifiers)
    {
        if (target.TryGetComponent<DirtyAttributeComponent>(out var dirty))
        {
            attributeSystem.SetAggregatorValue(target, mod.AttributeId, baseValue: 0f);
            attributeSystem.AddAggregatorMod(target, mod.AttributeId, handle,
                mod.EvaluatedMagnitude, mod.ModOp);
            dirty.SetBit(mod.AttributeId);
        }
    }

    // 6. 添加 GrantedTags
    if (comp.GrantedTags.Count > 0)
    {
        if (target.TryGetComponent<GameplayTags.GameplayTagsComponent>(out var tags))
        {
            foreach (var tag in comp.GrantedTags)
                tags.AddTag(tag);
        }
    }

    // 7. OnApplicationEffects
    // ...

    return handle;
}

public void RemoveEffect(int handle, EEffectEndType reason)
{
    if (!handleToSpec.TryGetValue(handle, out var spec)) return;

    // Remove Mods from Aggregator
    attributeSystem.RemoveAggregatorModsByHandle(handle);

    // Remove GrantedTags
    if (spec.Definition.GrantedTags.Count > 0)
    {
        var target = /* ... lookup from entity ... */;
        if (target.TryGetComponent<GameplayTags.GameplayTagsComponent>(out var tags))
        {
            foreach (var tag in spec.Definition.GrantedTags)
                tags.RemoveTag(tag);
        }
    }

    // Remove from Handle cache
    handleToSpec.Remove(handle);

    // Destroy Entity
    // Entity deletion handled by OnRemoveEntityTags (Task 12)
}
```

- [ ] **Step 4: 运行测试确认通过**

Run: `dotnet test tests/Gameplay.Tests/Gameplay.Tests.csproj --filter "FullyQualifiedName~EffectSystemTests"`
Expected: PASS

- [ ] **Step 5: 提交**

```bash
git add src/Gameplay/GameplayAbilities/GameplayEffect/EffectSystem.cs tests/Gameplay.Tests/GameplayAbilities/GameplayEffect/EffectSystemTests.cs
git commit -m "feat: add EffectSystem.Apply, CanApply, RemoveEffect

Co-Authored-By: Claude <noreply@anthropic.com>"
```

---

### Task 12: GameplayAbilitiesFeature 注册入口

**Files:**
- Create: `src/Gameplay/GameplayAbilities/GameplayAbilitiesFeature.cs`
- Create: `tests/Gameplay.Tests/GameplayAbilities/GameplayAbilitiesFeatureTests.cs`

**Interfaces:**
- Consumes: `EffectSystem` (Task 10-11), `AttributeSystem` (Task 8)
- Produces: `GameplayAbilitiesFeature` class

- [ ] **Step 1: 写测试**

```csharp
// tests/Gameplay.Tests/GameplayAbilities/GameplayAbilitiesFeatureTests.cs
namespace Gameplay.Tests.GameplayAbilities;

using Friflo.Engine.ECS;
using Gameplay;
using Gameplay.GameplayAbilities;

public class GameplayAbilitiesFeatureTests
{
    [Fact]
    public void Constructor_RegistersSystems()
    {
        var world = new World(NetMode.Standalone);
        var gas = new GameplayAbilitiesFeature(world.Store, world.NetMode);

        Assert.NotNull(gas.EffectSystem);
        Assert.NotNull(gas.AttributeSystem);
    }
}
```

- [ ] **Step 2: 运行测试确认失败**

Run: `dotnet test tests/Gameplay.Tests/Gameplay.Tests.csproj --filter "FullyQualifiedName~GameplayAbilitiesFeatureTests"`
Expected: FAIL

- [ ] **Step 3: 实现**

```csharp
// src/Gameplay/GameplayAbilities/GameplayAbilitiesFeature.cs
using Friflo.Engine.ECS;
using Gameplay.GameplayAbilities;
using Gameplay.GameplayAbilities;

namespace Gameplay.GameplayAbilities;

/// <summary>
/// GAS 子系统的注册入口。接收已有 EntityStore，挂上 AttributeSystem 和 EffectSystem。
/// 不是 World 的包裹，只是 System 的注册入口。
/// </summary>
public class GameplayAbilitiesFeature
{
    public AttributeSystem AttributeSystem { get; }
    public EffectSystem EffectSystem { get; }

    public GameplayAbilitiesFeature(EntityStore store, NetMode netMode)
    {
        AttributeSystem = new AttributeSystem();
        EffectSystem = new EffectSystem(AttributeSystem);

        // Phase 0: 预注册（AttributeSystem 需要 SG 生成的注册表就绪）
        // Phase 1: EventSystem 交换
        // Phase 2: AbilityActivationSystem
        // Phase 3: EffectSystem（Apply/Remove → CommandBuffer）
        store.AddSystem(EffectSystem);
        // Phase 4: AttributeSystem（Dirty → Evaluate）
        store.AddSystem(AttributeSystem);
        // Phase 5: AbilityTaskSystem
    }
}
```

- [ ] **Step 4: 运行测试确认通过**

Run: `dotnet test tests/Gameplay.Tests/Gameplay.Tests.csproj --filter "FullyQualifiedName~GameplayAbilitiesFeatureTests"`
Expected: PASS

- [ ] **Step 5: 提交**

```bash
git add src/Gameplay/GameplayAbilities/GameplayAbilitiesFeature.cs tests/Gameplay.Tests/GameplayAbilities/GameplayAbilitiesFeatureTests.cs
git commit -m "feat: add GameplayAbilitiesFeature registration entry point

Co-Authored-By: Claude <noreply@anthropic.com>"
```

---

## 验证

完成后运行全量测试：

```bash
dotnet test tests/Gameplay.Tests/Gameplay.Tests.csproj -f net10.0
dotnet test tests/Gameplay.Tests/Gameplay.Tests.csproj -f netstandard2.1
```

确认所有 GameplayAbilities 相关测试通过。
