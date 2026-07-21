# GameplayAbilities 框架设计

## 概述

GameplayAbilities 是 GAS（Gameplay Ability System）的核心模块，基于 ECS 架构，参考 UE5 GameplayAbilities 插件设计。所有代码放在 `src/Gameplay/GameplayAbilities/` 目录，命名空间 `Gameplay.GameplayAbilities`。

### 核心原则

- **坚持 UE GAS 思想，但不照搬 UE 实现**——ECS 的数据结构和执行模型与 UE 的 UObject 继承体系不同，需要重新设计
- **三层模式统一**——GameplayAbility 和 GameplayEffect 都采用"静态定义 → 实例数据 → 运行时 Entity"模式
- **持续状态走 GameplayEffect，一次性状态变化走直接 Commit**——不强制所有 Cost 都包装为 Instant GE
- **ECS 化**——Framework 层面的 System 遍历 Entity 驱动逻辑，但不预置具体游戏属性

---

## 模块总览

```
src/Gameplay/GameplayAbilities/
├── Attribute/                    # 属性系统
│   ├── IAttributeSetComponent    # 标记接口，标记 struct 为 AttributeSet
│   ├── GameplayAttribute         # 属性寻址句柄（Id + ComponentType + Offset）
│   ├── GameplayAttributeData     # 属性值容器（BaseValue + CurrentValue + ModifierBuffer）
│   ├── GameplayAttributeAttribute# [GameplayAttribute] 自定义 Attribute，编译期 SG 扫描
│   └── AttributeSystem           # Modifier 重算 System
├── GameplayEffect/               # 效果系统
│   ├── GameplayEffect            # 静态定义（非 Entity）
│   ├── GameplayEffectSpec        # 施放实例，一次性快照（非 Entity）
│   ├── ActiveGameplayEffect      # 运行时 Entity（Component 集合）
│   ├── GameplayModifier          # 修改器（GameplayAttribute + ModOp + MagnitudeCalc）
│   ├── GameplayEffectModifierMagnitude  # 幅度计算（ScalableFloat / AttributeBased / CustomCalculation / SetByCaller）
│   ├── StackingConfig            # 堆叠配置
│   └── EffectSystem              # Tick / 周期执行 / 过期 / 移除
├── Ability/                      # 能力系统
│   ├── GameplayAbility           # 静态定义（非 Entity）
│   ├── AbilitySpec               # 授予实例数据（非 Entity）
│   ├── AbilityCollectionComponent# 角色拥有的 Ability 集合
│   ├── ActiveAbilityComponent    # 运行时激活 Entity 的标记 Component
│   ├── AbilityActivationRequest  # 激活请求 Component
│   ├── AbilityActivationSystem   # 激活流程 System
│   ├── IAbilityRequirement       # CanActivate 扩展点
│   ├── IAbilityCommit            # Commit 扩展点
│   └── IAbilityExecutor          # Execute 扩展点
├── AbilityTask/                  # Ability 相关 Task 上下文
│   ├── AbilityTaskContextComponent# 关联到哪个 ActiveAbility
│   └── AbilityTaskSystem         # Task 完成检测、Cancel 传播
├── GameplayCue/                  # 表现系统
│   ├── GameplayCueManager        # POCO，Static/Burst 消息通道
│   ├── GameplayCueParameters     # Cue 参数
│   └── LoopingCueComponent       # Looping Cue 的 Entity Component
├── GameplayEvent/                # 事件系统
│   ├── GameplayEventComponent    # 瞬时 Event Entity Component
│   └── EventSystem               # 匹配 Tag → 分发给监听者 → 销毁
├── GameplayAbilityWorld          # 扩展 World，持有 CueManager 等全局设施
└── ...
```

### 与现有模块的关系

| 现有模块 | 依赖方式 |
|----------|---------|
| GameplayTags | 被所有子系统依赖（Tag 寻址、Tag 条件） |
| GameplayTasks | AbilityTask 的底层通用异步框架，不修改 |
| 状态同步 (Bubble) | ActiveGameplayEffect / ActiveAbility Entity 同步由 Bubble 统一处理 |
| 预测回滚 | 由状态同步层统一处理，GAS 不做重复预测逻辑 |

### 编译剔除

| 子系统 | Dedicated Server (`GP_SERVER`) |
|--------|-------------------------------|
| GameplayCue | 剔除 |
| Client 预测 | 剔除 |

---

## 模块一：Attribute 属性系统

### 三层结构

```
[编译期 Source Generator]
    │
    ▼
GameplayAttribute                  GameplayAttributeData              IAttributeSetComponent
(属性寻址句柄)                     (属性值容器)                       (标记接口)
─────────────────────────────────────────────────────────────────────────────────
• internal int id                  • float BaseValue                  • 标记 struct 为 AttributeSet
• internal Type SetType            • float CurrentValue               • 一个 Entity 可挂多个
• internal int offset              • ModifierBuffer (internal)        • 游戏层 struct 实现
• ref float GetValue(entity)       • 提供 ref 给 AttributeSystem 读写
• void SetValue(entity, value)
```

### GameplayAttributeData

框架唯一提供的属性值容器，不预设任何具体属性名：

```csharp
public struct GameplayAttributeData
{
    public float BaseValue;      // 永久基础值
    public float CurrentValue;   // 计算后的当前值 = BaseValue + Modifiers
}
```

### GameplayAttribute（寻址句柄）

由 Source Generator 编译期生成。游戏层写：

```csharp
public struct CombatAttributeSet : IAttributeSetComponent
{
    [GameplayAttribute]
    public GameplayAttributeData Health;

    [GameplayAttribute]
    public GameplayAttributeData MaxHealth;

    [GameplayAttribute]
    public GameplayAttributeData AttackPower;
}
```

SG 编译期扫描 `[GameplayAttribute]`，生成：
- 每个字段对应的 `GameplayAttribute` 静态句柄（Id + ComponentType + Offset）
- AttributeId → 访问委托的注册表（注册期缓存，热路径零反射）

### AttributeSystem

- 初始化期：SG 生成的注册表，收集所有 `IAttributeSetComponent` → 提取每个 `[GameplayAttribute]` 字段 → 生成 `GameplayAttribute` 句柄
- 热路径：遍历带 `AttributeDirtyTag` 的 Entity → 读取该 Entity 上的 ActiveGameplayEffect → 收集所有 Modifier → 按 `GameplayAttribute` 索引 → 应用 ModOp → 写回 `CurrentValue`

---

## 模块二：GameplayEffect 效果系统

### 三层模型

```
GameplayEffect                    GameplayEffectSpec                ActiveGameplayEffect
(静态定义，非 Entity)             (施放实例，非 Entity)              (运行时 Entity)
─────────────────────────────────────────────────────────────────────────────────
• DurationPolicy                   • 引用 GameplayEffect             • StartTime
  - Instant                         • Level                         • Duration（已计算）
  - HasDuration                    • Duration（已计算）              • PeriodProgress
  - Infinite                       • Period（已计算）                • StackCount
• Period（周期间隔）                • Modifiers[]（已计算 Magnitude）  • Handle
• Modifiers[]                      • SetByCallerMagnitudes           • PredictionKey
  - GameplayAttribute               • Source/Target Snapshot Tags     • bIsInhibited
  - ModOp                          • EffectContext
    · Add/Multiply/Override         • DynamicAssetTags
  - MagnitudeCalc                   └── 创建后不可变，应用前快照
    · ScalableFloat
    · AttributeBased
    · CustomCalculationClass
    · SetByCaller
• StackingConfig
  - StackLimit
  - DurationPolicy（Refresh/Extend/Never）
  - PeriodPolicy（Reset/Never）
  - ExpirationPolicy（ClearAll/RemoveOne/Refresh）
• TagRequirements
  - ApplicationRequired / Ongoing / Removal
• OverflowConfig
• GrantedTags（Effect 期间授予的 Tag）
• CueDefinitions（关联的 GameplayCue）
```

### GameplayEffectModifierMagnitude（幅度计算）

四种计算方式（与 UE 一致）：

| 类型 | 说明 |
|------|------|
| ScalableFloat | 简单数值乘法（系数×等级表值） |
| AttributeBased | 从 Source/Target 抓取属性计算 |
| CustomCalculationClass | 自定义计算类（可实现任意逻辑） |
| SetByCaller | 施放时动态传入 |

### EffectSystem

每帧遍历所有 `ActiveGameplayEffect` Entity：

```
1. CheckTagRequirements
   ├── Owner 的 Tag 不满足 OngoingRequirement？ → 标记为 Inhibited
   ├── Owner 的 Tag 满足 RemovalRequirement？   → 移除 Effect
   └── Inhibited 恢复（PeriodInhibitionRemovedPolicy）

2. TickDuration
   ├── Infinite → 跳过
   ├── Duration > 0 → Duration -= dt
   └── Duration ≤ 0 → 进入 Expiration

3. TickPeriod
   ├── 无 Period → 跳过
   ├── PeriodProgress += dt
   └── PeriodProgress ≥ Period → 执行 Modifiers → PeriodProgress -= Period

4. Expiration
   ├── StackCount > 1 → StackCount--（按 ExpirationPolicy）→ Refresh Duration
   └── StackCount = 1 → 移除 Entity
```

### 创建与查询

- **Apply**：`new GameplayEffectSpec(effectDef, context, level)` → `EffectSystem.Apply(spec)` → 创建 ActiveGameplayEffect Entity
- **查询**：`EffectSystem.GetActiveEffects(entity, query)` → 返回匹配的 Handle 列表
- **移除**：`EffectSystem.RemoveEffect(handle)` → 清理 Entity + 触发 RemovalCallbacks
- **同步**：ActiveGameplayEffect Entity 的 Component 变更由 Bubble 层统一处理

---

## 模块三：GameplayAbility 能力系统

### 三层模型（与 GameplayEffect 一致）

```
GameplayAbility                 AbilitySpec                     ActiveAbility
(静态定义，非 Entity)           (实例数据，非 Entity)            (运行时 Entity)
────────────────────────────────────────────────────────────────────────────
• AbilityTags                   • 引用 GameplayAbility          • StartTime
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

---

## 模块四：Activation Pipeline

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
│                                 │  ← 用户可扩展: CustomCommit
├─────────────────────────────────┤
│  3. Create ActiveAbility Entity │  ← 子 Entity，挂到 Owner 下
│     ActiveAbilityComponent      │
├─────────────────────────────────┤
│  4. Execute（能力逻辑）          │  ← 内置: ApplyEffectExecutor (对 Target 施加 GE Spec)
│     IAbilityExecutor            │  ← 内置: SpawnTaskExecutor (创建 Task Entity)
│     .Execute(activeAbilityCtx)  │  ← 用户可扩展: CustomExecutor
└─────────────────────────────────┘
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

### 模型

```
GameplayEvent
(瞬时 Entity)
─────────────────────────────────
• EventTag（GameplayTag）
• Instigator（Entity 引用）
• Target（Entity 引用）
• EventMagnitude（float）
• OptionalSource（来源 Ability / GameplayEffect）
• Payload（自定义数据）
• 生命周期：创建 → EventSystem 消费 → 销毁（≤ 1 帧）
```

### EventSystem

```
每帧 Tick：
1. 遍历所有 GameplayEvent Entity
2. 按 EventTag 匹配监听者：
   ├── ActiveAbility 的 AbilityTriggers 匹配 → 激活对应 Ability
   ├── ActiveGameplayEffect 的 EventHandler 匹配 → 回调处理
   └── 外部注册的 Listener 匹配 → 回调
3. 消费后销毁 Event Entity
```

### 触发路径

```
Ability.Execute()          → SendGameplayEvent(tag, payload, target)
GameplayEffect 组件        → 特定 GE 组件触发 Event
EffectSystem               → Effect Removed / Effect Expired Event
Attribute 变化              → 可选 AttributeChange Event
```

---

## 模块七：AbilityTask

### 分层关系

```
GameplayTask（通用异步框架，src/Gameplay/GameplayTasks/，不修改）
├── TaskStateComponent          (Pending → Running → Done → Cancelled)
├── TaskOwnerComponent          (哪个 Entity 拥有这个 Task)
├── DelayTaskComponent          (延时示例)
├── DelayTaskSystem
└── TaskSystem                  (通用推进)

AbilityTask（GameplayTask 的使用者，src/Gameplay/GameplayAbilities/AbilityTask/）
├── AbilityTaskContextComponent  (关联到哪个 ActiveAbility + Handle)
└── AbilityTaskSystem            (Task 完成检测、Cancel 传播)
```

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

## 全局扩展：GameplayAbilityWorld

扩展 World 以持有 GAS 子系统：

```csharp
public class GameplayAbilityWorld
{
    public EntityStore Store;           // Friflo ECS EntityStore
    public NetMode NetMode;

    public GameplayCueManager CueManager;     // POCO
    public EventSystem EventSystem;           // System
    public EffectSystem EffectSystem;          // System
    public AbilityActivationSystem ActivationSystem; // System
    public AbilityTaskSystem AbilityTaskSystem;     // System
    public AttributeSystem AttributeSystem;         // System
}
```

---

## 实施优先级

| 优先级 | 模块 | 理由 |
|--------|------|------|
| P0 | GameplayAttribute + GameplayAttributeData | 所有其他模块的基础 |
| P0 | GameplayEffect + GameplayEffectSpec + ActiveGameplayEffect + EffectSystem | Cooldown 和 Buff 依赖 |
| P1 | GameplayAbility + AbilitySpec + AbilityCollectionComponent | 依赖 Effect（Cooldown） |
| P1 | AbilityActivationSystem + Pipeline（Requirements/Commit/Execute） | 依赖上述 |
| P2 | GameplayEvent | Ability 间通信 |
| P2 | GameplayCue | 表现层，依赖 Event |
| P2 | AbilityTask（Context + System） | 异步 Ability 需要 |
| P3 | Source Generator（[GameplayAttribute] 扫描） | 编译期代码生成 |
| P3 | 网络同步（Bubble 集成） | 依赖所有模块稳定 |
