# GameplayAbilities 框架设计

## 概述

GameplayAbilities 是 GAS（Gameplay Ability System）的核心模块，基于 ECS 架构，参考 UE5 GameplayAbilities 插件设计。所有代码放在 `src/Gameplay/Gameplay.Abilities/` 目录，命名空间 `Gameplay.Abilities`。

### 核心原则

- **坚持 UE GAS 思想，但不照搬 UE 实现**——ECS 的数据结构和执行模型与 UE 的 UObject 继承体系不同，需要重新设计
- **三层模式统一**——GameplayAbility 和 GameplayEffect 都采用"静态定义 → 实例数据 → 运行时 Entity"模式
- **持续状态走 GameplayEffect，一次性状态变化走直接 Commit**——不强制所有 Cost 都包装为 Instant GE
- **ECS 化**——Framework 层面的 System 遍历 Entity 驱动逻辑，但不预置具体游戏属性

---

## 模块总览

```
src/Gameplay/Gameplay.Abilities/
├── Attribute/                    # 属性系统
│   ├── IAttributeSetComponent    # 标记接口，标记 struct 为 AttributeSet
│   ├── GameplayAttribute         # 属性寻址句柄（Id + ComponentType + Offset）
│   ├── GameplayAttributeData     # 属性值容器（BaseValue + CurrentValue）
│   ├── DirtyAttributeComponent   # 属性的脏标记（bitmask per-Entity）
│   ├── AttributeAggregator       # Mod 列表 + Evaluate（由 AttributeSystem 内部管理）
│   ├── GameplayAttributeAttribute# [GameplayAttribute] 自定义 Attribute，编译期 SG 扫描
│   └── AttributeSystem           # 脏属性重算 System
├── GameplayEffect/               # 效果系统
│   ├── GameplayEffect            # 静态定义（非 Entity）
│   ├── GameplayEffectSpec        # 施放实例，一次性快照（非 Entity）
│   ├── ActiveGameplayEffectComponent  # 运行时状态（所有字段合一）
│   ├── GameplayModifier          # 修改器
│   ├── GameplayEffectModifierMagnitude  # 幅度计算
│   ├── StackingConfig            # 堆叠配置
│   └── EffectSystem              # Tick / 周期执行 / 过期 / 移除
├── Ability/                      # 能力系统
│   ├── GameplayAbility           # 静态定义（非 Entity）
│   ├── AbilitySpec               # 授予实例数据（非 Entity）
│   ├── AbilityCollectionComponent# 角色拥有的 Ability 集合
│   ├── ActiveAbilityComponent    # 运行时激活 Entity 的标记 Component
│   ├── AbilityActivationRequest  # 激活请求（POCO Command，当前 Tick）
│   ├── AbilityActivationSystem   # 激活流程 System
│   ├── IAbilityRequirement       # CanActivate 扩展点
│   ├── IAbilityCommit            # Commit 扩展点
│   └── IAbilityExecutor          # Execute 扩展点
├── GameplayCue/                  # 表现系统
│   ├── GameplayCueManager        # POCO，Static/Burst 消息通道
│   ├── GameplayCueParameters     # Cue 参数
│   └── LoopingCueComponent       # Looping Cue 的 Entity Component
├── GameplayEvent/                # 事件系统（SourceGenerator + POCO）
│   ├── GameplayEventAttribute     # [GameplayEvent] 自定义 Attribute（SG 扫描）
│   ├── StructBuffer<T>            # 通用无 GC struct 缓冲（Add/GetRef/Reset）
│   ├── GameplayEventFrame         # 一帧事件的 Records + Payloads 封装
│   ├── GameplayEventBus           # 双缓冲 current/pending Frame
│   ├── GameplayEventId            # SG 生成的事件 ID 常量
│   ├── GameplayEventRegistry      # SG 生成的 Payload 元数据
│   ├── GameplayEventHandlerRegistry # SG 生成的 Handler 注册表
│   └── EventSystem                # 消费 Current → 匹配 ID → 分发 Handler
├── AbilityTask/                  # Ability 相关 Task 上下文
│   ├── AbilityTaskContextComponent# 关联到哪个 ActiveAbility
│   ├── AbilityTaskSystem         # Task 完成检测、Cancel 传播
│   ├── WaitDelayTask             # 等待 N 秒
│   ├── WaitGameplayEventTask     # 等待 GameplayEvent
│   ├── WaitAttributeChangeTask   # 等待属性变化
│   ├── WaitGameplayTagAddedTask   # 等待 Tag 添加
│   ├── WaitGameplayTagRemovedTask # 等待 Tag 移除
│   ├── WaitAbilityCommitTask     # 等待 Commit
│   └── WaitCancelTask            # 等待 Cancel
├── Prediction/                   # 预测系统
│   ├── IPredictionService         # Begin / Confirm / Reject 接口
│   ├── PredictionSystem           # Confirm/Reject 实现 + Rollback
│   └── NetExecutionPolicy         # LocalPredicted / LocalOnly / ServerOnly / ServerInitiated
├── GameplayAbilitiesFeature      # 注册入口，将 GAS System 挂到 EntityStore
└── ...
```

### 与现有模块 / 外部依赖的关系

| 模块 | 依赖方式 |
|------|---------|
| GameplayTags | 被所有子系统依赖（Tag 寻址、Tag 条件） |
| GameplayTasks | AbilityTask 的底层通用异步框架，不修改 |
| 状态同步 (Bubble) | ActiveGameplayEffect / ActiveAbility Entity 同步由 Bubble 统一处理；Bubble 实现 IPredictionService |
| Prediction（GAS 内部模块） | 定义 IPredictionService 接口 + PredictionKey 管理；网络层负责 RPC；Bubble 实现 Confirm/Reject |

### 编译剔除

| 子系统 | Dedicated Server (`GP_SERVER`) |
|--------|-------------------------------|
| GameplayCue（CueManager + LoopingCue） | 剔除——DS 无表现层 |
| PredictionSystem.ClientPredict() | 剔除——DS 无客户端预测，保留 Confirm/Reject 路径 |

---

## 模块一：Attribute 属性系统

### 结构

```
[编译期 Source Generator]
    │
    ▼
GameplayAttribute                  GameplayAttributeData              IAttributeSetComponent
(属性寻址句柄)                     (属性值容器，纯数据)               (标记接口)
─────────────────────────────────────────────────────────────────────────────────
• internal int id                  • float BaseValue                  • 标记 struct 为 AttributeSet
• internal int SetTypeId            • float CurrentValue               • 一个 Entity 可挂多个
• SG 生成强类型访问器              └── 干净的值类型，无 Aggregator     • 游戏层 struct 实现
  例: GetHealth(entity) → ref float
```

> **不用裸 offset：** Friflo 的 Archetype 搬移会改变 Component 内存布局，裸 offset 不稳定。SG 生成 Friflo 的 `entity.GetComponent<T>()` 或 `entity.Data<T>()` 访问器，零反射、编译期类型安全。

### GameplayAttributeData

框架唯一提供的属性值容器，不预设任何具体属性名，不嵌入 Aggregator：

```csharp
public struct GameplayAttributeData
{
    public float BaseValue;      // 永久基础值
    public float CurrentValue;   // 计算后的当前值 = Evaluate(BaseValue, Mods)
}
```

### GameplayAttribute（寻址句柄）

由 Source Generator 编译期生成。游戏层写：

```csharp
public struct CombatAttributeSet : IAttributeSetComponent
{
    [GameplayAttribute] public GameplayAttributeData Health;
    [GameplayAttribute] public GameplayAttributeData MaxHealth;
    [GameplayAttribute] public GameplayAttributeData AttackPower;
}
```

SG 编译期扫描 `[GameplayAttribute]`，生成：
- 每个字段对应的 `GameplayAttribute` 静态句柄（Id + ComponentType + Offset）
- AttributeId → 访问委托的注册表（注册期缓存，热路径零反射）

### AttributeAggregator（独立对象，AttributeSystem 内部管理）

参考 UE5 `FAggregator` 设计——Aggregator 不是 Component，不存储在 Entity 上，由 `AttributeSystem` 外部管理：

```
AttributeSystem 内部:
  Dictionary<(Entity, int attributeId), AttributeAggregator> aggregators;
```

```csharp
internal class AttributeAggregator
{
    public float BaseValue;
    public bool  Dirty;

    // Mod 列表（每个 Mod 保留 Handle、Magnitude、Op、TagReqs）
    // 按 ModOp 分桶：AddMods[], MultiplyMods[], DivideMods[], OverrideMods[], FinalAddMods[]
    internal List<ModEntry>[] ModBuckets;  // ModBuckets[(int)Op]

    public float Evaluate()
    {
        // 聚合公式（同 UE）:
        // if has Override → OverrideValue
        // else ((BaseValue + ΣAdd) × ΠMultiply / ΠDivide) + ΣFinalAdd
    }
}

internal struct ModEntry
{
    public int ActiveHandle;             // 归属的 ActiveGameplayEffect Handle
    public float Magnitude;              // 已计算的幅度
    public GameplayTagRequirement SourceTagReqs;
    public GameplayTagRequirement TargetTagReqs;
}
```

### DirtyAttributeComponent（per-Entity，bitmask）

SG 为每个 `[GameplayAttribute]` 分配唯一 `AttributeDirtyIndex`：

```csharp
public struct DirtyAttributeComponent : IComponent
{
    public ulong DirtyBits;   // Bit<i> = Attribute<i> needs re-evaluation
                              // 64 attributes per entity 覆盖所有实际场景；超过则 SG 编译报错
}
```

### 两层架构

```
EffectSystem (Apply / Remove / Period Execute)
    │
    │  for each Modifier in GE.Spec:
    │     aggregator = GetOrCreate(entity, mod.Attribute.Id)
    │     aggregator.ModBuckets[Op].Add(new ModEntry { Handle, Magnitude })
    │     aggregator.Dirty = true
    │     Entity.DirtyAttributeComponent.SetBit(attribute.DirtyIndex)
    ▼
AttributeAggregator (Mod 列表，Dirty 标记)
    │
    ▼
AttributeSystem Tick（只处理 DirtyBits）
    │
    │  for each set bit in DirtyBits:
    │     aggregator = aggregators[(entity, attributeId)]
    │     aggregator.Evaluate()
    │     → write GameplayAttributeData.CurrentValue
    │     aggregator.Dirty = false
    │  clear DirtyBits
    │
    ▼
```

### 复杂度

| 操作 | 复杂度 |
|------|--------|
| Apply Effect | O(ModifierCount) —— 每次 Mod 一次 List.Add |
| Remove Effect | O(TotalModsForThisAttribute) —— 按 Handle 遍历移除 |
| Period Execute | O(ModifierCount) —— 同 Apply |
| AttributeSystem Tick | O(DirtyAttributeCount) —— 只计算脏属性 |
| 空闲帧 | O(0) —— 无 dirty bits 时零遍历 |

---

## 模块二：GameplayEffect 效果系统

### 三层模型

```
GameplayEffect                    GameplayEffectSpec                ActiveGameplayEffect
(静态定义，非 Entity)             (施放实例，非 Entity)              (运行时 Entity)
─────────────────────────────────────────────────────────────────────────────────
GameplayEffect 类（见下方）        GameplayEffectSpec 类（见上方）     ActiveGameplayEffectComponent
• DurationPolicy                  • Definition 引用                 • Duration / StartWorldTime
• Stacking 策略                   • Level / Duration / Period       • PeriodProgress / StackCount
• Modifiers[]（配置）             • Modifiers[]（已计算 Magnitude）  • Handle / SourceEntity / TargetEntity
• Tag 条件 / 副作用配置           • SetByCallerMagnitudes           • Definition 回引用
• Immunity/Remove 查询            • CapturedSource/TargetTags       • IsInhibited / PredictionKey
• CueDefinitions                  • EffectContext                   • 行为配置（Tag/Grant/Block/Cancel）
                                  • DynamicAssetTags                • RealTimeModifierInfo[]
```

### GameplayEffectSpec（施放实例，非 Entity）

```csharp
public class GameplayEffectSpec
{
    public GameplayEffect Definition;         // 静态定义引用
    public float Level;                       // 施放等级
    public float Duration;                    // 已计算的 Duration
    public float Period;                      // 已计算的 Period
    public FModifierSpec[] Modifiers;         // 已计算 Magnitude 的 Modifier 列表
    public Dictionary<GameplayTag, float> SetByCallerMagnitudes; // SetByCaller 传入值
    public GameplayTagContainer CapturedSourceTags;  // 施放时 Source 的 Tag Snapshot
    public GameplayTagContainer CapturedTargetTags;  // 施放时 Target 的 Tag Snapshot
    public GameplayTagContainer DynamicAssetTags;    // 运行时附加的 AssetTag
    public GameplayEffectContext EffectContext;      // 施放上下文（Instigator 等）
}
```

创建后不可变——Modifier 的 Magnitude 在构造时计算完成，期间不变（除 RealTime 策略外）。

### GameplayEffectRegistry（静态定义索引）

所有 `GameplayEffect` 在启动时注册，分配 `DefinitionId`。运行时 Entity 只存 ID 通过 Registry 查表：

```csharp
public static class GameplayEffectRegistry
{
    public static int Register(GameplayEffect def);
    public static GameplayEffect Get(int definitionId);
}
```

### GameplayModifier（Attribute + ModOp + MagnitudeCalc + CapturePolicy）

```csharp
public class GameplayEffect
{
    // ── 基础 ──
    public EGameplayEffectDurationType DurationPolicy; // Instant / HasDuration / Infinite
    public int StackLimit;
    public EGameplayEffectStackingDurationPolicy StackingDurationPolicy;
    public EGameplayEffectStackingPeriodPolicy StackingPeriodPolicy;
    public EGameplayEffectStackingExpirationPolicy StackingExpirationPolicy;
    public float Period;
    public EGameplayEffectPeriodInhibitionRemovedPolicy InhibitionPolicy;

    // ── Modifiers ──
    public GameplayModifier[] Modifiers;              // Attribute + ModOp + MagnitudeCalc + CapturePolicy

    // ── Tag 条件 ──
    public GameplayTagContainer ApplicationRequiredTags;
    public GameplayTagContainer OngoingRequiredTags;
    public GameplayTagContainer RemovalTags;

    // ── 副作用 ──
    public GameplayTagContainer GrantedTags;               // Effect 期间授予 Target 的 Tag
    public GameplayTagContainer BlockedAbilityTags;         // 阻止的 Ability Tag
    public GameplayTagContainer CancelAbilityTags;          // 取消的 Ability Tag
    public AbilitySpecConfig[] GrantedAbilities;            // 授予的 Ability

    // ── 其他 ──
    public float ChanceToApply;
    public Func<...> CustomCanApply;
    public GameplayEffectQuery[] ImmunityQueries;
    public GameplayEffectQuery[] RemoveOtherEffectsQueries;
    public ConditionalGameplayEffect[] OnApplicationEffects;
    public ConditionalGameplayEffect[] OnCompleteEffects;

    // ── Cue ──
    public GameplayEffectCue[] CueDefinitions;
}
```

### StackingConfig（复用 UE 完整策略）

| 维度 | 枚举值 | 行为 |
|------|--------|------|
| DurationPolicy | RefreshOnSuccessfulApplication | 新 Stack → Duration 重置为 GE 定义值 |
| | NeverRefresh | 新 Stack → 保持原有剩余时间不变 |
| | ExtendDuration | 新 Stack → Duration += 新 Duration |
| PeriodPolicy | ResetOnSuccessfulApplication | 新 Stack → PeriodProgress 归零 |
| | NeverReset | 新 Stack → 保持原有周期进度 |
| ExpirationPolicy | ClearEntireStack | Duration 到期 → 清空所有层数，销毁 Entity |
| | RemoveSingleStackAndRefreshDuration | 到期 → StackCount--，Duration 刷新 |
| | RefreshDuration | 到期 → StackCount--，Duration 刷新，永不超时（手动管理） |

System 层面实现：
- **Apply 时**：检查 Target 下是否有同源 GE Entity → 有则 StackCount++（按 DurationPolicy / PeriodPolicy 处理时间），无则新建
- **Expiration 时**：按 ExpirationPolicy 决定衰减还是直接销毁
- **StackLimit**：超过上限拒绝新 Stack 或执行溢出策略

### Modifier 的 CapturePolicy 与 RealTime 机制

与 UE 一致，每个 Modifier 标记 Magnitude 的抓取策略：

```csharp
public enum EAttributeCapturePolicy
{
    Snapshot,   // Spec 创建时抓取一次，Magnitude 期间不变（默认，性能最优）
    RealTime,   // 每次执行时从 Source/Target 实时重新抓取属性
}

public struct GameplayModifier
{
    public GameplayAttribute Attribute;
    public EGameplayModOp ModOp;
    public GameplayEffectModifierMagnitude MagnitudeCalc;
    public EAttributeCapturePolicy CapturePolicy;   // Snapshot / RealTime
    public EModifierExecutionType ExecutionType;       // Persistent / ExecuteOnApply / ExecuteOnPeriod

    public GameplayTagRequirement SourceTagReqs;
    public GameplayTagRequirement TargetTagReqs;
}

/// <summary>Modifier 执行类型——避免 Period 重复累加。</summary>
public enum EModifierExecutionType
{
    Persistent,         // Apply → 注册 Aggregator；Remove → 移除（Duration/Buff/Debuff）
    ExecuteOnApply,     // Apply 时执行一次，不注册 Aggregator（Instant GE 专用）
    ExecuteOnPeriod,    // 每次 Period 执行一次，不注册为持续 Modifier（DOT/HOT）
}
```

**Snapshot（默认）：**
```
Spec 创建时:
  for each Modifier:
    if CapturePolicy == Snapshot:
      Magnitude = CalcMagnitude(sourceEntity, targetEntity, level, setByCaller)
      → 固化在 Spec 中，后续 Tick 复用
```

**RealTime：**
```
Spec 创建时:
  for each Modifier:
    if CapturePolicy == RealTime:
      记录 MagnitudeCalc + 引用的 Source/Target Attribute
      → 不固化 Magnitude

每次 Execute (Apply / Period):
  for each RealTime Modifier:
    Magnitude = 重新 CalcMagnitude(sourceEntity, targetEntity, level, setByCaller)
    → 反映 Source/Target 当前属性变化

Source/Target 属性变化时:
  Aggregator.Evaluate() 重新计算 → 写 CurrentValue
  → AttributeSystem 查反向索引:
      reverseIndex[(sourceEntity, attributeId)] → 受影响的 ActiveEffectHandle 列表
  → 标记这些 ActiveEffect 的 RealTimeModifier 需要重新计算 Magnitude
  → 标记 Target Entity 对应 Attribute Dirty

// 反向索引（AttributeSystem 内部维护，Apply 时注册，Remove 时注销）:
// Dictionary<(Entity sourceEntity, int attributeId), List<int>> activeEffectHandleIndex;
```

### ActiveGameplayEffectComponent（运行时 Entity，单一 Component）

所有运行时状态合并到一个 Component，避免 Archetype 碎片化和多 Component 创建开销：

```csharp
public struct ActiveGameplayEffectComponent : IComponent
{
    // ── 时间 ──
    public float Duration;                       // 剩余时间（Infinite = -1），EffectSystem 每帧递减
    public float StartWorldTime;                 // 开始时间戳——用于 GetTimeRemaining() 查询和 Server→Client 时间同步

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
    public int DefinitionId;                     // GameplayEffectRegistry 查表 key（避免托管引用 blittable 问题）

    // ── 抑制与预测 ──
    public bool IsInhibited;                     // Tag 条件不满足时 = true
    public EGameplayEffectPeriodInhibitionRemovedPolicy InhibitionPolicy;
    public PredictionKey PredictionKey;

    // ── 行为配置（从 GameplayEffect 静态定义拷贝，NULL/empty = 不适用）──
    public GameplayTagContainer ApplicationRequiredTags;
    public GameplayTagContainer OngoingRequiredTags;
    public GameplayTagContainer RemovalTags;
    public GameplayTagContainer GrantedTags;
    public GameplayTagContainer BlockedAbilityTags;
    public GameplayTagContainer CancelAbilityTags;
    public AbilitySpecConfig[] GrantedAbilities;

    // ── RealTime Modifier 信息 ──
    // 每个 RealTime Modifier 需要记录 MagnitudeCalc + 引用的源属性
    // 以便每次 Execute 时重新计算 Magnitude
    public RealTimeModifierInfo[] RealTimeModifiers;    // NULL = 全是 Snapshot
}
```

### EffectSystem

单一 System，内部按职责分 private 方法。每帧遍历所有 `ActiveGameplayEffectComponent` Entity。

> **结构变更规则：** System Tick 中不直接 CreateEntity / DestroyEntity。Apply 和 Remove 产生的 Entity 变更写入 `CommandBuffer`（待创建/待销毁列表），Tick 结束后在 `PostUpdate` 中批量执行。防止 Query 遍历期间结构修改破坏迭代器。

```
1. CheckTagRequirements
   ├── Owner Tag 不满足 OngoingRequiredTags → 标记 IsInhibited
   ├── Owner Tag 满足 RemovalTags → 移除 Effect
   └── Inhibited 恢复（InhibitionPolicy）

2. TickDuration
   ├── Infinite → 跳过
   ├── Duration > 0 → Duration -= dt
   └── Duration ≤ 0 → 进入 Expiration

3. TickPeriod
   ├── Period ≤ 0 → 跳过
   ├── PeriodProgress += dt
   └── PeriodProgress ≥ Period → 执行 Modifiers → PeriodProgress -= Period

4. Expiration
   ├── StackCount > 1 → StackCount--（按 StackingExpirationPolicy）→ Refresh Duration
   └── StackCount = 1 → 清理 GrantedTag / GrantedAbility → 销毁 Entity
```

### GameplayEffectModifierMagnitude（幅度计算）

四种计算方式（与 UE 一致）：

| 类型 | 说明 |
|------|------|
| ScalableFloat | 简单数值乘法（系数×等级表值） |
| AttributeBased | 从 Source/Target 抓取属性计算 |
| CustomCalculationClass | 自定义计算类（可实现任意逻辑） |
| SetByCaller | 施放时动态传入 |

### EffectSystem Apply 流程（PreApply → CanApply → Apply）

```
Apply(spec, target)             ← 入口
    │
    ├── 1. PreApply（清理冲突，无资格检查）
    │       └── RemoveOtherEffects: 遍历 Target ActiveGE
    │           → 匹配 spec.GameplayEffect.RemoveOtherEffectsQueries
    │           → 找到则 RemoveEffect(oldHandle)
    │
    ├── 2. CanApply（纯检查，无副作用）
    │       ├── ApplicationRequiredTags
    │       ├── Immunity: 遍历 Target ActiveGE → GameplayEffectRegistry.Get(comp.DefinitionId).ImmunityQueries
    │       │   → 任一匹配 spec → 返回 false
    │       ├── ChanceToApply
    │       └── CustomCanApply
    │       → 返回 bool
    │
    ├── 3. Create / Stack ActiveGameplayEffect Entity
    │       └── Stacking 检查 → 新建或更新 StackCount
    │
    └── 4. Apply 副作用:
            ├── GrantedTags / GrantedAbilities / BlockedAbilityTags / CancelAbilityTags
            ├── for each Modifier → Aggregator.AddMod() → SetBit(Dirty)
            ├── CueDefinitions
            └── OnApplicationEffects（连锁 Apply 其他 GE）

enum EEffectEndType
{
    Normal,       // Duration 自然到期、StackCount 降为零
    Premature,    // RemoveEffect() 主动移除 / RemoveOtherEffects 冲突 / RemovalTags 触发 / Cancel
}
```

**Instant GE** 的行为：`DurationPolicy == Instant` 时，不创建 ActiveGameplayEffect Entity，Apply 只执行一次 Modifiers + Cue，然后立刻进入 RemoveEffect 逻辑（清理 GrantedTags 等瞬时副作用，不涉及 Duration Tick）。

### RemoveEffect 流程（EffectSystem.Remove 入口 + Expiration 触发）

```
RemoveEffect(handle, reason)
    │
    ├── 按 reason 决定是否触发 OnCompleteEffects
    │     - Normal: Duration 自然到期
    │     - Premature: 被 RemoveEffect() 主动移除 / RemoveOtherEffects / RemovalTags 触发
    │
    ├── Remove GrantedTags / GrantedAbilities
    ├── Remove Mods from Aggregator → SetBit(Dirty)
    ├── OnCompleteEffects（链接触发其他 GE）
    └── Destroy Entity
```

**设计原则：** Immunity / RemoveOtherEffects / OnCompleteEffects 均为 GameplayEffect 静态定义的字段，不映射为 Runtime Component。

### TagSource 引用计数

GrantedTag 来自多个源（ActiveGE、ActiveAbility、外部代码），不能简单 `AddTag`/`RemoveTag`。每个来源必须独立计数：

```csharp
// GameplayTagsComponent 内部
internal Dictionary<GameplayTag, int> tagRefCounts; // Tag → 授予该 Tag 的来源数

public void AddTag(GameplayTag tag)
{
    tagSet.Set(tag.id);
    if (tagRefCounts.TryGetValue(tag, out int count))
        tagRefCounts[tag] = count + 1;
    else
        tagRefCounts[tag] = 1;
}

public void RemoveTag(GameplayTag tag)
{
    if (!tagRefCounts.TryGetValue(tag, out int count)) return;
    count--;
    if (count <= 0)
    {
        tagRefCounts.Remove(tag);
        tagSet.Clear(tag.id);  // 所有来源都移除了才真正清位
    }
    else
        tagRefCounts[tag] = count;
}
```

ActiveGE/ActiveAbility 移除时只调用 `RemoveTag` → 只减少自己来源的计数 → 不影响其他源。

### 创建与查询

- **CanApply**：`EffectSystem.CanApply(spec, target)` → bool，纯检查
- **Apply**：`EffectSystem.Apply(spec, target)` → 创建 Entity 或 Stack，有副作用
- **Remove**：`EffectSystem.RemoveEffect(handle, reason)` → 按 Handle 移除 Mod 并清理 → 可能触发 OnCompleteEffects
- **查询**：`EffectSystem.GetActiveEffects(entity, query)` → 返回匹配的 Handle 列表
- **同步**：ActiveGameplayEffect Entity 的 Component 变更由 Bubble 层统一处理

---

## 模块三：GameplayAbility 能力系统

### 三层模型（与 GameplayEffect 一致）

```
GameplayAbility                 AbilitySpec                     ActiveAbility
(静态定义，非 Entity)           (实例数据，非 Entity)            (运行时 Entity)
────────────────────────────────────────────────────────────────────────────
• AssetTags                     • 引用 GameplayAbility          • StartTime
• CancelAbilitiesWithTag        • Level                         • Handle
• BlockAbilitiesWithTag         • InputID                       • PredictionKey
• ActivationOwnedTags           • SourceObject                  • bIsActive
• ActivationRequiredTags        • RemovalPolicy                 • CurrentActorInfo
• ActivationBlockedTags         • SetByCallerTagMagnitudes      • ActiveTasks → Task Entity 子级
• SourceRequired/BlockedTags    └── 存在 AbilityCollectionComp  └── 激活时创建，结束时销毁
• TargetRequired/BlockedTags
• Cooldown GE 引用
• AbilityTriggers[]             AbilityCollectionComponent
  - TriggerTag                  (挂在 Owner Entity 上)
  - TriggerSource               ───────────────────────────────
• NetExecutionPolicy            • AbilitySpec[] Specs
• NetSecurityPolicy             └── 该 Entity 拥有的能力列表
```

### AbilityCollectionComponent

角色拥有的能力集合，直接存在 Owner Entity 上：

```csharp
public struct AbilityCollectionComponent : IComponent
{
    public AbilitySpec[] Specs;   // 预分配或动态扩展
}
```

这与 InventoryComponent、EquipmentComponent 等价——都是角色的稳定状态描述。

### GameplayAbility 静态定义（关键字段）

对照 UE 源码，从 `UGameplayAbility` 提取框架需要的字段：

```csharp
public class GameplayAbility
{
    // ── Tags ──
    public GameplayTagContainer AssetTags;              // 此 Ability 自身的 Tag
    public GameplayTagContainer CancelAbilitiesWithTag; // 激活时取消持有这些 Tag 的 Ability
    public GameplayTagContainer BlockAbilitiesWithTag;  // 激活期间阻止这些 Tag 的 Ability
    public GameplayTagContainer ActivationOwnedTags;    // 激活期间给 Owner 添加的 Tag
    public GameplayTagContainer ActivationRequiredTags; // Owner 必须有全部这些 Tag
    public GameplayTagContainer ActivationBlockedTags;  // Owner 有任一 Tag 就阻止
    public GameplayTagContainer SourceRequiredTags;     // Instigator 的 Tag 条件
    public GameplayTagContainer SourceBlockedTags;
    public GameplayTagContainer TargetRequiredTags;     // Target 的 Tag 条件
    public GameplayTagContainer TargetBlockedTags;

    // ── Cooldown ──
    public GameplayEffect CooldownEffect;               // Duration GE 引用（冷却期间添加 Cooldown Tag）

    // ── Triggers ──
    public AbilityTriggerData[] AbilityTriggers;        // { TriggerTag, TriggerSource }
                                                       // TriggerSource: GameplayEvent / OwnedTagAdded / OwnedTagPresent

    // ── Network ──
    public EGameplayAbilityNetExecutionPolicy NetExecutionPolicy;  // LocalPredicted / LocalOnly / ServerInitiated / ServerOnly
    public EGameplayAbilityNetSecurityPolicy NetSecurityPolicy;    // ClientOrServer / ServerOnlyExecution / ServerOnlyTermination / ServerOnly

    // ── Extensions ──
    public IAbilityRequirement[] Requirements;          // CanActivate 检查（扩展点）
    public IAbilityCommit[] CommitActions;              // Commit 操作（扩展点: ApplyCooldown / ConsumeCost / Custom）
    public IAbilityExecutor Executor;                   // Execute 逻辑（扩展点）
}
```

**设计决策——为什么不照搬 UE 的 Cost/CoolDown 双 GE 模式：**

| | UE | ECS 设计 | 理由 |
|---|----|---------|------|
| Cooldown | GameplayEffect（Duration + GrantedTag） | **同样：GameplayEffect** | Duration 天然适合 GE，免费获得 Refresh/Extend/Stack/Remove/Reduction |
| Cost | GameplayEffect（Instant，修改属性） | **直接 Commit** | 一次性 MCP（Mana/HP/Ammo）不需要 Instant GE 的完整管道 |

**原则：持续状态走 GameplayEffect，一次性状态变化走 Commit。**

### AbilitySpec

对应 UE 的 `FGameplayAbilitySpec`，存在 `AbilityCollectionComponent` 中：

```csharp
public struct AbilitySpec
{
    public GameplayAbility Ability;          // 静态定义引用
    public int Level;                        // 能力等级
    public int InputID;                      // 输入绑定（可选）
    public object SourceObject;               // 来源对象（谁授予的）
    public EGrantedAbilityRemovePolicy RemovalPolicy; // 移除策略
    public Dictionary<GameplayTag, float> SetByCallerTagMagnitudes; // 授予时参数
    public GameplayTagContainer DynamicSpecSourceTags; // 运行时附加 Tag
}
```

### ActiveAbilityComponent（运行时 Entity，单一 Component）

激活时创建子 Entity 挂此 Component，结束时销毁：

```csharp
public struct ActiveAbilityComponent : IComponent
{
    public float StartTime;                        // 激活时间戳
    public int Handle;                             // 全局唯一 ID
    public PredictionKey PredictionKey;           // 预测 Key
    public bool IsActive;                          // 是否激活中
    public Entity Owner;                           // 归属的 Owner Entity
    public int DefinitionId;                       // AbilityDefinitionRegistry 查表 key
    public EAbilityInstanceState State;             // Activating / Active / Ending / Cancelled / Completed
}

public enum EAbilityInstanceState
{
    Activating, Active, Ending, Cancelled, Completed
}
```

---

## 模块四：Activation Pipeline

### Command vs Fact：两条路径汇入同一 Pipeline

```
Command（当前 Tick）                         Fact（GameplayEventBus, Deferred）
─────────────────────                        ───────────────────────────────
Input System                                  GameplayEventBus (PendingBuffer)
    │                                              │
AI Decision                                        │
    │                                         Frame N: Pending → Current
Network RPC                                         │
    │                                         AbilityTriggerSystem
    │                                         匹配 Ability.AbilityTriggers
    │                                              │
    │                                              ▼
    │                                  创建 AbilityActivationRequest
    │                                    (Source = GameplayEvent)
    │                                              │
    └──────────────────────┬───────────────────────┘
                           ▼
                 AbilityActivationRequest  ← 唯一入口
                 { Owner, SpecHandle, Target, Source }
                           │
                           ▼
                 AbilityActivationSystem
                           │
                           ▼
                 Requirements → Commit → Execute
```

```csharp
public struct AbilityActivationRequest
{
    public Entity Owner;
    public int SpecHandle;             // AbilityCollectionComponent 中的索引
    public Entity Target;
    public EActivationSource Source;   // Input / AI / GameplayEvent / Network
}

public enum EActivationSource { Input, AI, GameplayEvent, Network, TagTrigger }
```

> **为什么用 SpecHandle 而非 GameplayTag？** 同 Owner 可能有多个相同 Tag 的 Ability（不同来源/Level）。`SpecHandle` 精确定位。**

**核心原则：**

**核心原则：**
- `AbilityActivationRequest` = Command（"请执行 X"），当前 Tick 消费
- `GameplayEvent` = Fact（"世界发生了 Y"），PendBuffer → 下一 Tick 消费
- GameplayEvent 可以触发 ActivationRequest（经 TriggerSystem），但两者不能合并

### 整体流程

```
AbilityActivationSystem (每帧 Tick)
    │
    │  收到激活请求（外部调用或 AbilityTrigger 匹配）
    │
    ▼
┌─────────────────────────────────┐
│  1. Requirements（纯检查，无副作用）│  ← 任一失败则中止，记录失败原因
│     IAbilityRequirement[]       │  ← 被内部置: TagRequirement, AttributeRequirement
│     .Evaluate(owner, spec, ctx) │  ← 用户可扩展: CustomRequirement
├─────────────────────────────────┤
│  2. Commit（消耗 + 冷却）        │  ← Requirements 全部通过后执行
│     IAbilityCommit[]            │  ← 内置: ApplyCooldownCommit (施加 Cooldown GE)
│     .Execute(owner, spec, ctx)  │  ← 内置: ConsumeCostCommit (直接 Modify Attribute)
│                                 │  ← 内置: CancelAbilitiesWithTag / BlockAbilitiesWithTag
│                                 │  ← 用户可扩展: CustomCommit
├─────────────────────────────────┤
│  3. Create ActiveAbility Entity │  ← 子 Entity，挂到 Owner 下
│     ActiveAbilityComponent      │
├─────────────────────────────────┤
│  4. Execute（能力逻辑）          │  ← 内置: ApplyEffectExecutor (对 Target 施加 GE Spec)
│     IAbilityExecutor            │  ← 内置: SpawnTaskExecutor (创建 Task Entity)
│     .Execute(activeAbilityCtx)  │  ← 用户可扩展: CustomExecutor
└─────────────────────────────────┘
    │  ⚠️ Execute 失败 → 回滚 Commit:
    │      ├── Remove Cooldown GE
    │      ├── 退还 Cost Attribute
    │      ├── 不移除 ActivationOwnedTags（ApplyCooldownCommit 已施加 → 回退）
    │      └── 不创建 ActiveAbility Entity
    │
    │  框架根据 ActiveTasks 自动判断结束时机：
    │
    ├── Execute 后有 Task Entity 子级 → 等所有 Task Done/Cancelled
    └── Execute 后无 Task Entity 子级 → 立即 EndAbility
    │
    ▼
  ActiveAbility Entity 销毁
    ├── 级联销毁 Task Entity（子 Entity 跟随）
    ├── 移除 ActivationOwnedTags
    ├── 解除 BlockAbilitiesWithTag（归还阻止标记）
    ├── 移除 Looping Cue
    └── 触发 OnAbilityEnded Callback
```

### 与 UE 的 Pipeline 对比

| UE | ECS | 说明 |
|----|-----|------|
| TryActivateAbility() | AbilityActivationSystem 入口 | 外部调用或 Trigger 匹配 |
| CanActivateAbility() | Requirements[].Evaluate() | Tag + Attribute 检查，无副作用 |
| CheckCost() | AttributeRequirement.Evaluate() | 只检查不扣 |
| CheckCooldown() | TagRequirement.Evaluate() | Cooldown GE 已施加的 Tag 即阻止 |
| CommitExecute() | Commit[].Execute() | 有副作用：扣属性、加冷却 |
| ApplyCooldown() | ApplyCooldownCommit | 施加 Cooldown GE |
| ApplyCost() | ConsumeCostCommit | 直接 Modify Attribute |
| ActivateAbility() | IAbilityExecutor.Execute() | 用户定义的能力逻辑 |
| EndAbility() | System 自动判断 | Tasks 全 Done 或 Cancel 时触发 |

### Cancel 流程

```
外部 Cancel → AbilityActivationSystem.CancelAbility(handle)
    │
    ├── AbilityTaskSystem 级联 Cancel 所有子 Task Entity
    ├── 移除 ActivationOwnedTags
    ├── 清理 Looping Cue
    ├── 调用 OnAbilityEnded(bWasCancelled=true)
    └── 销毁 ActiveAbility Entity
```

---

## 模块五：GameplayCue 表现系统

### 混合模型

```
                          GameplayCue
                              │
              ┌───────────────┴───────────────┐
              ▼                               ▼
        Static / Burst                    Looping
        (消息通道，非 ECS)                (Entity)
        ─────────────────                ─────────
        触发 → CueManager.AddCue()       触发 → 创建 LoopingCue Entity（子 Entity）
              │                               │
              ▼                               ▼
        GameplayCueManager 查找 Handler  LoopingCueSystem 每帧 Tick
              │                               │
              ▼                               ▼
        Handler.Execute(params)          跟随 ActiveEffect / ActiveAbility 生命周期
              │                          父 Entity 销毁 → 级联清理
              ▼
        立即完成 (Burst 可带 Magnitude)
```

### GameplayCueManager（POCO）

```csharp
// 核心操作
CueManager.AddCue(tag, params, entity)
    // Static  → 立即调 Handler.Execute(params)，无状态
    // Burst   → 立即调 Handler.Execute(params)，带 Magnitude
    // Looping → 创建 LoopingCue Entity + 注册

CueManager.RemoveCue(tag, entity)      // 移除单个 Looping Cue
CueManager.RemoveAllCues(entity)       // 清理该 Entity 所有 Looping Cue
```

### LoopingCueComponent

```csharp
struct LoopingCueComponent : IComponent
{
    GameplayTag CueTag;
    float ElapsedTime;
    // Cue 参数...
}
```

### Cue 触发来源

| 来源 | 方式 |
|------|------|
| GameplayAbility.Execute() | 直接调 CueManager.ExecuteCue / AddCue / RemoveCue |
| GameplayEffect 定义 | GE.CueDefinitions[] 定义什么 Tag + Level → Normalize → Magnitude |
| 任意游戏代码 | 直接调 CueManager |

### 编译剔除

```csharp
#if GP_SERVER
    CueManager = NullCueManagerInstance;  // Dedicated Server 无表现
#endif
```

---

## 模块六：GameplayEvent 事件系统

### 设计理念：SourceGenerator Event Schema

不照搬 UE 的 `FGameplayEventData` + runtime `object` Payload。用 C# 编译期能力替代 UE UObject Reflection——每种事件类型产生编译期生成的 ID、Serializer、Handler 注册表，运行时零 GC、零反射。

### 定义 Schema（用户代码）

```csharp
[GameplayEvent(Tag = "Event.Damage", Payload = typeof(DamagePayload))]
public partial struct DamageEvent { }

public struct DamagePayload
{
    public float Amount;
    public DamageType DamageType;
    public Entity Source;
}

[GameplayEvent(Tag = "Event.Death", Payload = typeof(DeathPayload))]
public partial struct DeathEvent { }

public struct DeathPayload
{
    public Entity Killer;
    public DeathReason Reason;
}
```

开发者只声明结构体 + Attribute，SG 处理其余部分。

### Source Generator 生成

```
编译期:  DamageEvent.cs → GameplayEventGenerator → 生成:
─────────────────────────────────────────────────────────
1. EventId:
   public enum GameplayEventId : ushort
   {
       Damage,
       Death,
   }

2. GameplayEventRegistry（Payload 元数据，用于网络/编辑器/Replay）:
   public static class GameplayEventRegistry
   {
       public static readonly EventInfo[] Events = {
           new(Id=1, PayloadSize=sizeof(DamagePayload), Tag="Event.Damage"),
           new(Id=2, PayloadSize=sizeof(DeathPayload), Tag="Event.Death"),
       };
   }

3. Handler 自动注册:
   [HandlesGameplayEvent("Event.Damage")]
   public partial class ShieldAbilityTrigger { }
   → 生成 GameplayEventHandlerRegistry[Damage] = [ShieldAbilityTrigger.Handle]

4. Serializer（网络/Bubble 集成）:
   WriteDamageEvent(ref Writer w, DamagePayload data)
   ReadDamageEvent(ref Reader r) → DamagePayload
```

### StructBuffer<T>（框架内置，零 GC）

```csharp
public sealed class StructBuffer<T> where T : unmanaged
{
    private T[] buffer;
    private int count;

    public int Count => count;

    public int Add(in T value)
    {
        if (count >= buffer.Length) Grow();
        buffer[count] = value;
        return count++;
    }

    public ref T GetRef(int index) => ref buffer[index];

    public void Reset() { count = 0; }  // 只重置计数，不清内存
}
```

动态增长，每帧 `Reset()` 只设 `count=0`，无内存清零开销。

### GameplayEventFrame + GameplayEventBus

`GameplayEventFrame` 封装同一帧的 Records + Payloads，保证 Swap 时原子同步：

```csharp
// SG 生成的 partial，每种 [GameplayEvent] 生成一个 StructBuffer 字段
public sealed partial class GameplayEventFrame
{
    public StructBuffer<GameplayEventRecord> Records;
    public StructBuffer<DamagePayload> DamagePayloads;
    public StructBuffer<DeathPayload> DeathPayloads;
    // ...

    public void Reset()
    {
        Records.Reset();
        DamagePayloads.Reset();
        DeathPayloads.Reset();
        // ...
    }
}

public sealed class GameplayEventBus
{
    private GameplayEventFrame current;
    private GameplayEventFrame pending;

    public void Swap() { (current, pending) = (pending, current); }
}

// Frame: Swap → EventSystem.Consume(current) → current.Reset()
```

### GameplayEventRecord

```csharp
public struct GameplayEventRecord
{
    public ushort EventId;
    public Entity Source;
    public Entity Target;
    public float Magnitude;
    public int PayloadIndex;       // 对应 Typed Buffer 的索引
}
```

### Handler 系统：Static + Dynamic 两套并存

```
GameplayEventBus
       │
       ├── Static Handler Registry（SG 生成）
       │     [EventId] → InvokeTable
       │     例: [Damage] → DamageSystem, QuestSystem, CombatLog...
       │     启动时注册，永久不变
       │
       └── Dynamic Listener Registry（Runtime）
             [EventId] → DynamicListener[]
             例: [Damage] → TaskEntity 100, TaskEntity 200...
             运行时 Register / Remove，Generation Handle
```

**复杂度：O(Event Listeners)，不是 O(Task × Event)。**

#### Static Handler（SG `[HandlesGameplayEvent]`）

```csharp
[HandlesGameplayEvent("Event.Damage")]
public partial class DamageSystem { }

// SG 生成:
// StaticHandlerRegistry[GameplayEventId.Damage] = [DamageSystem, ShieldTrigger, ...]
```

#### Dynamic Listener（Runtime）

```csharp
// 注册（WaitGameplayEventTask.Start 时调用）
ListenerHandle handle = eventBus.Register(
    GameplayEventId.Damage,
    taskEntity, handlerId   // handlerId → InvokeTable[handlerId]
);

// 注销（Task Done/Cancelled 时）
eventBus.Remove(handle);
```

```csharp
// Dynamic Listener 内部存储
internal struct DynamicListener
{
    public ushort EventId;
    public Entity Owner;
    public int HandlerId;
    public ushort Version;
}
```

Generation Handle + Swap Remove，防止 Entity 销毁后的野引用。

#### HandlerId + InvokeTable（SG 生成，零 delegate/interface）

```
SG 为每个 [GameplayEventListener] 生成 HandlerId + InvokeTable:
  InvokeTable[handlerId] = static (record, payload, owner) => { ... }
```

### 消费（EventSystem）

```
for each record in current.Records:
  // 1. Static Handlers
  for handler in StaticRegistry[record.EventId]:
    handler.Handle(record, payload)

  // 2. Dynamic Listeners
  for listener in DynamicRegistry[record.EventId]:
    InvokeTable[listener.HandlerId](record, payload, listener.Owner)
```

**为什么不用 byte Arena：** Typed Buffer 消除对齐问题、Unsafe cast，Cache 最优。

### 事件时效规则

| 类型 | 时机 | 理由 |
|------|------|------|
| GameplayEvent | PendBuffer → 下一 Tick | 避免递归、确定性、零 GC |
| AbilityActivationRequest | 当前 Tick | Command，需要立即反馈 |
| GameplayCue (Burst/Static) | 当前 Tick | 纯表现 |

### 触发路径

```
Ability.Execute()          → EventBus.Enqueue(new DamageEvent{...})
GameplayEffect            → 特定 GE 触发 Event
EffectSystem              → Effect Removed / Expired Event
Attribute 变化             → AttributeChange Event（可选）
```

---

## 模块七：AbilityTask

### 分层关系

```
GameplayTask（通用异步框架，src/Gameplay/Gameplay.Tasks/，不修改）
├── TaskStateComponent          (Pending → Running → Done → Cancelled)
├── TaskOwnerComponent          (哪个 Entity 拥有这个 Task)
├── DelayTaskComponent          (延时示例)
├── DelayTaskSystem
└── TaskSystem                  (通用推进)

AbilityTask（GameplayTask 的使用者，src/Gameplay/Gameplay.Abilities/AbilityTask/）
├── AbilityTaskContextComponent  (关联到哪个 ActiveAbility + Handle)
├── AbilityTaskSystem            (Task 完成检测、Cancel 传播)
├── WaitDelayTask                (等待 N 秒)
├── WaitGameplayEventTask        (等待特定 GameplayEvent)
├── WaitAttributeChangeTask      (等待属性变化)
├── WaitGameplayTagTask          (等待 Tag 添加/移除)
├── WaitAbilityCommitTask        (等待 Commit 事件)
└── WaitCancelTask               (等待 Cancel 信号)
```

### 内置 Task 类型

| Task | 用途 | 优先级 |
|------|------|--------|
| WaitDelay | 等待 N 秒后回调 | P0 |
| WaitGameplayEvent | 等待匹配 EventTag 的 GameplayEvent | P0 |
| WaitCancel | 等待 Ability 被 Cancel | P0 |
| WaitAttributeChange | 等待指定 GameplayAttribute 的 CurrentValue 变化 | P1 |
| WaitGameplayTagAdded | 等待 Owner 获得指定 Tag | P1 |
| WaitGameplayTagRemoved | 等待 Owner 移除指定 Tag | P1 |
| WaitAbilityCommit | 等待 Commit 事件（用于异步 Ability 的 Commit 时机） | P1 |

### AbilityTaskContextComponent

```csharp
struct AbilityTaskContextComponent : IComponent
{
    Entity ActiveAbility;        // 所属的 ActiveAbility Entity
    AbilityTaskHandle Handle;   // 用于回调定位
}
```

### AbilityTaskSystem

不负责推进 Task（那是 TaskSystem 的职责），只负责 Ability 相关的编排：

```
1. ActiveAbility Execute 后，监听其下所有 Task Entity 的 TaskState
2. 所有 Task 进入 Done/Cancelled → 触发 EndAbility
3. ActiveAbility 被 Cancel → AbilityTaskSystem 级联 Cancel 所有子 Task Entity
4. ActiveAbility Entity 销毁 → 子 Task Entity 跟随级联销毁
```

### Entity 层级关系

```
ActiveAbility Entity (Owner)
    ├── ActiveAbilityComponent
    └── Task Entity (子 Entity)
        ├── TaskStateComponent           ← GameplayTask 通用
        ├── TaskOwnerComponent           ← GameplayTask 通用
        └── AbilityTaskContextComponent   ← AbilityTask 新增
```

---

## 模块八：Prediction 预测

### 三层架构

```
GAS 层                          网络层                           Prediction Bridge
(PredictionKey 管理)             (RPC / Bubble / Snapshot)        (IPredictionService)
─────────────────────────        ────────────────────────         ──────────────────────
• PredictionKey                  • RPC                           • interface IPredictionService
• NetExecutionPolicy             • Replication                   • 由 Bubble 实现
• 预测对象 Entity 标记            • Bubble 状态同步
• 不接触 Socket/RPC               • GAS 不知道如何发包
```

### IPredictionService（Bridge 接口）

```csharp
public interface IPredictionService
{
    PredictionKey Begin();                    // 开始一个预测事务
    void Confirm(PredictionKey key);          // Server 确认
    void Reject(PredictionKey key);           // Server 拒绝
}
```

网络层（Bubble）注入实现，GAS 只依赖接口。

### PredictionSystem（GAS 内部）

```csharp
// 注册期注入
PredictionSystem.Service = bubbleImpl;

// Confirm:
//   找到所有带 PredictionKey 的 Entity
//     → ActiveAbility 标记 Confirmed
//     → ActiveGameplayEffect 保留（从预测变为权威）
//     → GameplayCue 保留

// Reject:
//   找到所有带 PredictionKey 的 Entity
//     → 销毁 Entity
//     → AttributeAggregator 回滚（Remove Mods with matching PredictionKey Handle）
//     → 恢复 Attribute 值
```

### NetExecutionPolicy

```csharp
public enum EGameplayAbilityNetExecutionPolicy
{
    LocalOnly,           // 只在本地执行，不通知 Server
    LocalPredicted,      // Client 立即执行 → RPC 到 Server → Confirm/Reject
    ServerOnly,          // 只在 Server 执行
    ServerInitiated,     // Server 发起，Client 本地也执行
}
```

`AbilityActivationSystem` 根据 Policy 决定执行路径，但实际的 RPC 发送由 Network Adapter 负责。

### LocalPredicted 流程

```
Client:                                  Server:
───────                                  ───────
TryActivateAbility("Fireball")
NetExecutionPolicy == LocalPredicted
    │
    ├── key = PredictionService.Begin()
    ├── 本地执行完整 Pipeline
    ├── ActiveAbility.PredictionKey = key
    ├── ActiveGameplayEffect.PredictionKey = key
    ├── 本地播放 GameplayCue
    └── Network Adapter → RPC 到 Server
                                            │
                                            ├── 收到 RPC
                                            ├── AbilityActivationSystem 验证
                                            ├── Requirements 检查
                                            ├── 通过: Commit + Execute
                                            │     PredictionService.Confirm(key)
                                            └── 拒绝: PredictionService.Reject(key)

Client 收到 Confirm:
    PredictionSystem.Confirm(key)
    → 所有 Entity 的 PredictionKey 标记为 Confirmed

Client 收到 Reject:
    PredictionSystem.Reject(key)
    → 销毁预测 Entity
    → Aggregator 回滚
    → 播放取消 Cue
```

### 与模块的关系

| 模块 | 预测相关字段 |
|------|------------|
| ActiveAbilityComponent | PredictionKey |
| ActiveGameplayEffectComponent | PredictionKey |
| LoopingCueComponent | PredictionKey |

---

## 注册入口：GameplayAbilitiesFeature

不是 World 的包裹，只是将 GAS 的 System 和 Manager 注册到已有 `EntityStore` 的入口：

```csharp
public class GameplayAbilitiesFeature
{
    public GameplayCueManager CueManager { get; }

    public GameplayAbilitiesFeature(EntityStore store, NetMode netMode)
    {
        CueManager = CreateCueManager(netMode);

        // 执行顺序很重要——按 Phase 注册（Friflo 按 AddSystem 顺序执行）
        // Phase 1: Event 交换
        store.AddSystem(new EventSystem());             // Pending → Current
        // Phase 2: Ability 激活
        store.AddSystem(new AbilityActivationSystem());
        // Phase 3: GE Apply / Remove
        store.AddSystem(new EffectSystem(attrSys));     // Duration Tick + Period + Exec
        // Phase 4: Attribute 重算
        store.AddSystem(attrSys);                       // Dirty → Evaluate → CurrentValue
        // Phase 5: Task 推进
        store.AddSystem(new AbilityTaskSystem());
    }
}
```

使用方式：

```csharp
var world = new World(NetMode.Server);
var gas = new GameplayAbilitiesFeature(world.Store, world.NetMode);
// gas.CueManager 由外部持有引用，用于触发 Cue
```

---

## 实施优先级

| 优先级 | 模块 | 理由 |
|--------|------|------|
| P0 | GameplayAttribute + GameplayAttributeData + AttributeAggregator + DirtyAttributeComponent | 所有其他模块的基础 |
| P0 | GameplayEffect + GameplayEffectSpec + ActiveGameplayEffectComponent + EffectSystem | Cooldown 和 Buff 依赖 |
| P1 | GameplayAbility + AbilitySpec + AbilityCollectionComponent + ActiveAbilityComponent | 依赖 Effect（Cooldown） |
| P1 | AbilityActivationSystem + Pipeline（Requirements/Commit/Execute）| 依赖上述 |
| P2 | GameplayEvent（Schema + EventBus + EventSystem + SG）| Ability 间通信 |
| P2 | GameplayCue + CueManager | 表现层 |
| P2 | AbilityTask（Context + System + 内置 Tasks）| 异步 Ability 需要 |
| P2 | Prediction（IPredictionService + PredictionSystem）| 客户端预测 |
| P3 | Source Generator（[GameplayAttribute] + [GameplayEvent] 扫描）| 编译期代码生成 |
| P3 | 网络同步（Bubble 集成）| 依赖所有模块稳定 |
| P3 | NetExecutionPolicy（RPC 路径）| 依赖 Prediction 和 Bubble 就绪 |
