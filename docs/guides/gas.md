# GAS（Gameplay Ability System）运转指南

Gameplay.NET 的 GAS 子系统基于 **ECS + 命令模式 + 事件驱动**，不设 UE 式巨型中枢组件（ASC），Entity 挂哪些 Component 就具备哪些 GAS 能力。

## 每帧运行流程

`GameplayAbilitiesFeature.Update(deltaTime)` 按 5 个阶段执行：

| 阶段 | 内容 | 说明 |
|------|------|------|
| Phase 0 | Event 交换+分发 | `EventDispatcher.Tick()` — 消费本帧事件 |
| Phase 1 | Task 推进 | Delay / WaitEvent / WaitAttr / WaitTag / WaitCommit System |
| Phase 2 | AbilityTask 完成检测 | `AbilityTaskSystem` — 全部 Task Done → CancelAbility |
| Phase 3 | GE Duration/Period | `EffectSystem.OnUpdate()` — 计时、周期执行、过期 |
| Phase 4 | Attribute 刷新 | `AggregatorManager.Flush()` — 统一 Evaluate + 写回 CurrentValue |
| Phase 5 | 延迟删除 | `ActivationManager.ProcessPendingDeletions()` |

---

## 运行流程图

```mermaid
graph TD
    subgraph 每帧入口
        U["GameplayAbilitiesFeature.Update(dt)"] --> P0
    end

    subgraph Phase0["Phase 0 · 事件分发"]
        P0["EventDispatcher.Tick()"]
        SWAP["Swap 取出 pending 帧"]
        DISP["遍历事件记录"]
        STATIC["→ 静态 Handler<br/>IGameplayEventHandler"]
        DYNAMIC["→ 动态 Listener<br/>Entity 上的 Handler"]
        P0 --> SWAP --> DISP --> STATIC
        DISP --> DYNAMIC
    end

    subgraph Phase123["Phase 1~3 · ECS SystemRoot.Update"]
        P1["Phase 1 · Task 推进"]
        DELAY["DelayTaskSystem<br/>计时递减"]
        WAIT_EVT["WaitGameplayEventTaskSystem<br/>事件匹配"]
        WAIT_ATTR["WaitAttributeChangeTaskSystem<br/>属性变化检测"]
        WAIT_TAG["WaitGameplayTagTaskSystem<br/>Tag 增删检测"]
        WAIT_COMMIT["WaitAbilityCommitTaskSystem<br/>Commit 完成检测"]
        P1 --> DELAY & WAIT_EVT & WAIT_ATTR & WAIT_TAG & WAIT_COMMIT
        PENDING["Pending Task → Running → Done"]

        P2["Phase 2 · Ability 完成检测"]
        ATS["AbilityTaskSystem"]
        CHECK["遍历 ActiveAbility 子 Entity<br/>所有 Task 都 Done/Cancelled？"]
        CANCEL["CancelAbility<br/>移除 OwnedTags · 清理子 Entity"]
        P2 --> ATS --> CHECK -->|是| CANCEL
        CHECK -->|否| NOOP["等待下一帧"]

        P3["Phase 3 · Effect Tick"]
        ES["EffectSystem.OnUpdate"]
        TICK_DUR["TickDuration<br/>Duration -= dt"]
        EXPIRE["到期？"]
        EXP_STACK["Stack 减一<br/>或 RemoveEffect"]
        TICK_PER["TickPeriod<br/>PeriodProgress += dt"]
        PER_TRIG["周期触发<br/>ExecutePeriodicModifiers"]
        DEFER["延迟 Apply 队列<br/>OnCompleteEffects 链接触发"]
        P3 --> ES --> TICK_DUR --> EXPIRE -->|是| EXP_STACK
        EXPIRE -->|否| TICK_PER --> PER_TRIG
        ES --> DEFER
    end

    subgraph Phase4["Phase 4 · 属性刷新"]
        P4["AttributeAggregatorManager.Flush"]
        FLUSH["遍历脏队列<br/>Evaluate 聚合公式"]
        WRITE["写回 CurrentValue"]
        P4 --> FLUSH --> WRITE
    end

    subgraph Phase5["Phase 5 · 清理"]
        P5["ActivationManager<br/>ProcessPendingDeletions"]
        DEL["删除延迟队列中的<br/>ActiveAbility Entity"]
        P5 --> DEL
    end

    U --> P0 --> P1
    P1 --> P2 --> P3 --> P4 --> P5

    style U fill:#4a90d9,color:#fff
    style P0 fill:#7b68ee,color:#fff
    style P1 fill:#50a050,color:#fff
    style P2 fill:#50a050,color:#fff
    style P3 fill:#50a050,color:#fff
    style P4 fill:#d4a055,color:#fff
    style P5 fill:#c05050,color:#fff
```

### Ability 激活流程

```mermaid
graph TD
    REQ["AbilityActivationRequest<br/>Owner + AbilitySpecHandle"] --> FIND["查 AbilityCollectionComponent<br/>获取 AbilitySpec"]
    FIND --> TAG_CHECK["内置 TagRequirement"]
    TAG_CHECK --> REQ_LIST["遍历 IAbilityRequirement[]<br/>逐条 Evaluate()"]
    REQ_LIST -->|任一失败| REJECT["❌ 拒绝激活"]
    REQ_LIST -->|全部通过| COMMIT["遍历 IAbilityCommit[]<br/>逐条 Execute()<br/>扣 Cost · 上 CD"]
    COMMIT --> ACTIVE["创建 ActiveAbility Entity<br/>Owner 子 Entity<br/>ActiveAbilityComponent"]
    ACTIVE --> EXEC["IAbilityExecutor.Execute()<br/>ApplyEffect · Spawn · Task"]
    EXEC -->|异常| ROLLBACK["回滚 Commit<br/>删除 ActiveAbility Entity<br/>❌ 激活失败"]
    EXEC -->|成功| TAGS["添加 ActivationOwnedTags<br/>到 Owner"]
    TAGS --> DONE["✅ 激活成功"]

    style REJECT fill:#c05050,color:#fff
    style ROLLBACK fill:#c05050,color:#fff
    style DONE fill:#50a050,color:#fff
```

### Effect Apply / Remove 流程

```mermaid
graph TD
    APPLY["EffectSystem.Apply(spec, target)"]
    PRE["PreApply<br/>RemoveOtherEffects 查询<br/>移除冲突 GE"]
    STACK{"同源 GE 已存在？<br/>同 Definition"}
    STACK_OP["Stack 叠加<br/>刷新 Duration"]
    CAN{"CanApply?<br/>RequiredTags · Immunity · Chance"}
    CREATE["创建 ActiveGE Entity<br/>target 子 Entity"]
    MOD["施加 Modifier<br/>Persistent → Aggregator<br/>ExecuteOnApply → BaseValue"]
    GRANT["添加 GrantedTags"]
    CHAIN["OnApplicationEffects<br/>链接触发"]
    DONE_A["✅ Apply 完成"]

    APPLY --> PRE --> STACK
    STACK -->|是| STACK_OP --> DONE_A
    STACK -->|否| CAN
    CAN -->|否| REJECT_A["❌ 拒绝"]
    CAN -->|是| CREATE --> MOD --> GRANT --> CHAIN --> DONE_A

    REMOVE["EffectSystem.RemoveEffect(handle, reason)"]
    REM_MOD["移除 Aggregator 中所有 Mod"]
    REM_TAGS["移除 GrantedTags"]
    COMPLETE["OnCompleteEffects<br/>链接触发 → 延迟队列"]
    DESTROY["销毁 ActiveGE Entity<br/>级联子 Entity"]
    DONE_R["✅ Remove 完成"]

    REMOVE --> REM_MOD --> REM_TAGS --> COMPLETE --> DESTROY --> DONE_R

    style REJECT_A fill:#c05050,color:#fff
    style DONE_A fill:#50a050,color:#fff
    style DONE_R fill:#50a050,color:#fff
```

### Task 生命周期

```mermaid
stateDiagram-v2
    [*] --> Pending: Executor 创建 Task Entity
    Pending --> Running: TaskSystem 检测条件满足
    Running --> Done: 任务完成（延迟到、事件到达...）
    Running --> Cancelled: Ability 被 Cancel
    Done --> [*]: AbilityTaskSystem 清理
    Cancelled --> [*]: AbilityTaskSystem 清理

    state 全部完成 {
        Done
        Cancelled
    }
```

### Attribute 聚合公式

```mermaid
graph TD
    INPUT["BaseValue<br/>+ Persistent Mods 桶"]
    OVER{"Override 桶<br/>有值？"}
    R_OVER["返回最后一个 Override 值"]
    ADD["Base + ΣAdditive"]
    MUL["× ΠMultiply"]
    DIV["/ ΠDivide"]
    FA["+ ΣFinalAdd"]
    RESULT["返回聚合结果"]
    CUR["写回 CurrentValue"]

    INPUT --> OVER
    OVER -->|是| R_OVER --> CUR
    OVER -->|否| ADD --> MUL --> DIV --> FA --> RESULT --> CUR

    style R_OVER fill:#d4a055,color:#fff
    style RESULT fill:#50a050,color:#fff
```

---

## 六大子系统

### 1. GameplayTag — 层级标签

`GameplayTag` 是轻量 `int` 句柄，由 `GameplayTagManager` 维护全局层级树。核心操作：

- **精确匹配** `MatchesExact` — id 相等
- **层级匹配** `Matches` — 此 Tag 是 parent 或其子孙（如 `Damage.Fire` 匹配 `Damage`）
- **位集查询** `GameplayTagSet` — 用 `long[]` 位集，`HasAny`/`HasAll` 均为位运算

`GameplayTagContainer` 封装位集，手写 `struct Enumerator` 避免 `foreach` 装箱。

### 2. GameplayAttribute — 属性聚合

每个属性是独立 Component（如 `HealthAttribute`、`ManaAttribute`），Entity 自身即 AttributeSet。

`AttributeAggregatorManager` 内部管理 `Dictionary<AttributeKey, AttributeAggregator>`：

- **BaseValue**：属性基准值
- **Modifier 桶**：按 `Additive` / `Multiply` / `Divide` / `Override` / `FinalAdd` 分桶存储
- **Evaluate 公式**：

```
Override 存在 → 返回最后一个 Override 值
否则 → ((Base + ΣAdd) × ΠMul / ΠDiv) + ΣFinalAdd
```

- **双缓冲脏队列**：修改标记 Dirty → `Flush()` 统一 Evaluate + 写回 CurrentValue

### 3. GameplayEffect — 效果系统

`GameplayEffect` 是静态定义（策划配置的数据资产），`EffectSystem` 是每帧 Tick 的 ECS System。

#### Apply 流程

1. **PreApply** — 按 `RemoveOtherEffectsQueries` 移除冲突 GE
2. **Stacking** — 同源 GE 叠加 StackCount，按策略刷新 Duration
3. **CanApply** — `ApplicationRequiredTags` + `ChanceToApply` 随机 + Immunity 查询
4. **创建 ActiveGE Entity** — 作为 target 的子 Entity
5. **Modifier 施加** — `Persistent` 注册到 Aggregator；`ExecuteOnApply` 直接改 BaseValue
6. **GrantedTags** — 添加授权标签到 target
7. **OnApplicationEffects** — 链接触发其他 GE

#### Remove 流程

- 从 Aggregator 移除所有 Modifier
- 移除 GrantedTags
- 触发 OnCompleteEffects 链接触发
- 销毁 ActiveGE Entity（级联子 Entity）

#### Duration 策略

| 策略 | 行为 |
|------|------|
| `Instant` | 立即执行 Modifier 后销毁 |
| `HasDuration` | Duration 递减至 0 后到期 |
| `Infinite` | 永不自动到期，需手动移除 |

### 4. GameplayAbility — 技能激活

`GameplayAbility` 是静态定义，通过三大接口驱动激活流程：

| 接口 | 阶段 | 特点 |
|------|------|------|
| `IAbilityRequirement.Evaluate()` | 条件检查 | 纯函数，无副作用 |
| `IAbilityCommit.Execute()` | 副作用提交 | 扣 Cost、上 CD，可回滚 |
| `IAbilityExecutor.Execute()` | 执行体 | Apply Effect、Spawn 等实际逻辑 |

**激活流程**：`Request → 查 AbilitySpec → Requirements 检查 → Commit 提交 → 创建 ActiveAbility Entity → Executor 执行（异常则回滚 Commit）`

### 5. AbilityTask — 异步任务

Executor 内部创建 Task Entity，挂在 ActiveAbility Entity 下，每个 Task 有 `TaskStateComponent` 状态机：

```
Pending → Running → Done / Cancelled
```

`AbilityTaskSystem` 检测到**全部 Task 都 Done/Cancelled** 时调用 `CancelAbility` 结束 Ability。

支持的 Task：`DelayTask`、`WaitGameplayEventTask`、`WaitAttributeChangeTask`、`WaitGameplayTagTask`、`WaitAbilityCommitTask`

### 6. GameplayEvent — 事件总线

遵循事件驱动模式，跨系统解耦：

- **生产**：`GameplayEventBus.Enqueue(in record)` → 写入 pending 帧
- **消费**：`EventDispatcher.Tick()` → `Swap` 取出当前帧 → 分发到注册的 Handler
  - **静态 Handler**：`IGameplayEventHandler` 接口，全局生效
  - **动态 Listener**：Entity 上的 Handler，按 `(entityId, handlerId)` 注册/注销

---

## 整体协作示例：火球技能

```
1. 输入触发 → ActivationManager.TryActivateAbility("Fireball")
2. Requirements: CD 可用？蓝量够？Tag 允许？→ 通过
3. Commit: CostCommit 扣蓝量 → CooldownCommit 上 CD
4. Executor: ApplyEffectExecutor
   → EffectSystem.Apply(damageGE, target)       // 瞬时伤害
   → EffectSystem.Apply(burnBuffGE, target)     // 持续灼烧
   → 创建 WaitDelayTask(1s) → 1s 后 CancelAbility
5. 每帧 EffectSystem Tick burnBuffGE:
   → Duration 递减
   → Period 触发 ExecuteOnPeriod 伤害
6. Task 1s 后 Done → AbilityTaskSystem 检测全部完成 → CancelAbility
7. EventBus 发出伤害/治疗事件 → UI 更新、死亡检查等系统响应
```

## 关键设计决策

- **无 ASC**：不建 `AbilitySystemComponent`，Entity 挂哪些 Component 就有哪些 GAS 能力
- **Effect 即 Entity**：GE 运行时实例是 target Entity 的子 Entity
- **命令模式**：Requirement / Commit / Executor 均为接口，可自由组合
- **数据驱动**：GE 和 Ability 都是静态配置资产，代码只定义结构和执行流程
- **POCO + System 混用**：多 Entity 遍历用 ECS System；单例管理用 POCO
