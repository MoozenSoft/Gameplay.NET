# 项目架构

## ECS

Gameplay.dll 基于 [Friflo.Engine.ECS](https://github.com/friflo/Friflo.Engine.ECS) 构建。

**不是所有功能都进 ECS**。分界线：多 Entity 批量遍历 → Component + System；单例服务 / 消息路由 / 基础设施 → 普通对象（POCO）。

## 功能蓝图

### ECS 域（Component + Entity + System）

| 优先级 | 功能 | 实现 |
|--------|------|------|
| 必须 | GAS | 见下方 GAS 节 |
| 必须 | 状态同步 | 见下方状态同步节 |
| 高 | Team | `TeamComponent`（阵营 ID），System 做友伤过滤、目标选择 |
| 高 | PlayerState | 挂在玩家 Entity 上的 Component 集合（PlayerID、Name、Score） |
| 高 | Spawn | `SpawnSystem` 读 `SpawnPointComponent` → 按 Archetype 创建 Entity |
| 高 | Movement | `MovementSystem` 遍历 `VelocityComponent` + `PositionComponent`，走/跑/跳/飞由 Component 组合区分 |
| 中 | Character | 不建 Character 类，**Archetype 约定**：Entity 挂 `InputComponent` + `MovementComponent` + `AbilityComponent` 即 Character |
| 中 | Inventory | Item = Entity（层次结构挂到背包 Entity 下），拾取/丢弃/装备 = System 改 Entity parent |

> **Input 边界**：`InputComponent` 是 ECS 与非 ECS 的桥梁——Input Service（非 ECS）采集设备输入写入 Component，System（ECS）只读 Component、不碰设备。

### 非 ECS 域（POCO / 独立服务）

| 优先级 | 功能 | 实现 |
|--------|------|------|
| 必须 | Input | 独立输入服务，采集设备输入，写入 `InputComponent` 供 System 消费 |
| 必须 | RPC | 独立网络层，`RpcService.Send(target, payload)` / 接收端分发到对应 Handler |
| 必须 | GameMode | Server Only POCO，事件驱动规则判定（OnKill → Score++ → CheckWin），不进 Entity |
| 必须 | GameState | Server 权威 POCO，GameMode 写入、RPC 下推客户端只读副本。不进 ECS、不走帧同步 |
| 工具 | Cheat/Console | 独立命令系统，注册/解析命令字符串 → 修改 Component 或调用 RPC |

### 运行位置

| | Server | Client | Host |
|------|:---:|:---:|:---:|
| ECS World | ✅ | ✅ | ✅ |
| Bubble 管理 | ✅ | ❌ | ✅ |
| GameMode | ✅ | ❌ | ✅ |
| GameState（权威） | ✅ | ❌ | ✅ |
| GameState（镜像） | ❌ | ✅ | ✅（同引用） |
| Input Service | ❌ | ✅ | ✅ |
| RPC Service | ✅ | ✅ | ✅ |
| Console | ✅ | ✅ | ✅ |

## GAS（Gameplay Ability System）

ECS 原生版 GAS——无巨型中枢组件，能力通过 Component 组合标记，逻辑由 System 驱动。

### 概念映射

| 概念 | ECS 实现 | 说明 |
|------|----------|------|
| **GameplayAttribute** | Component | 每个属性独立 Component（`HealthAttribute`、`ManaAttribute`），Entity 自身即 AttributeSet |
| **GameplayAbility** | Component（配置） + System（逻辑） | `AbilityComponent` 存标签/消耗/CoolDown 配置；`AbilityActivationSystem` 执行激活 |
| **GameplayEffect** | Entity + Component | 瞬时或持续 Effect 作为独立 Entity（挂 `DurationComponent`、`ModifierComponent`），`EffectSystem` 每帧遍历、修改 Attribute |
| **GameplayTag** | Component（标签集） | `TagComponent` 存层级标签（`State.Stunned`、`Ability.Attack.Melee`），用于分类与条件 |
| **GameplayEvent** | Entity（瞬时） | Event 触发 → 创建 Entity（挂 `EventTypeComponent` + `PayloadComponent`） → `EventSystem` 消费 → 销毁 |
| **AbilityTask** | Component（状态机） | 异步等待逻辑（`WaitDurationComponent`、`WaitEventComponent`），`TaskSystem` 驱动推进 |
| **GameplayCue** | Entity + Component | 表现层特效（仅 Client 端 `CueSystem` 处理，`GP_SERVER` 剔除） |
| **Prediction** | 复用状态同步层 | GAS 不复现预测逻辑，由状态同步层的 Bubble/预测回滚统一处理 |

### 核心设计

**无 ASC（AbilitySystemComponent）**：UE5 GAS 的 `UAbilitySystemComponent` 是单体枢纽，ECS 版拆散——Entity 上有哪些 GAS Component 就具备哪些 GAS 能力。

```
Entity[Player]
  ├─ HealthAttribute       ← Component
  ├─ ManaAttribute         ← Component
  ├─ TagComponent          ← Component (State.Alive, Team.Red)
  ├─ AbilityComponent[0]   ← Component (Fireball 配置)
  ├─ AbilityComponent[1]   ← Component (Dash 配置)
  └─ EffectEntity[Poison]  ← 子 Entity（Duration + Modifier）

System 遍历：
  AbilityActivationSystem  → 处理输入 → 激活 Ability → 创建 Effect Entity
  EffectSystem             → 遍历 Effect Entity → 修改 Attribute
  EventSystem              → 匹配 Event Entity → 分发到监听 System
  TaskSystem               → 推进 WaitDuration / WaitEvent → 触发回调
  CueSystem                → [Client only] 响应事件播放特效
```

**Effect 即 Entity**：Buff/Debuff、Dot/HoT、一次性伤害——都是 Entity。`EffectSystem` 每帧处理：Duration 递减 → Modifier 施加 → 到期销毁。不再区分 Instant/Duration/Infinite Effect 子类，靠 Component 组合区分。

**Event 即 Entity**：伤害事件、治疗事件、施法事件——瞬时 Entity，由 `EventSystem` 匹配 Tag → 分发到监听 System，消费后销毁。

**预测由状态同步层统一处理**：Ability 激活时，Client 本地创建预测 Effect Entity；服务端权威状态到达后，由状态同步的 reconciliation 机制回滚（参见"状态同步"节）。GAS 层不做重复预测逻辑。

## 状态同步

采用**服务端权威**模型，配合客户端预测/回滚。

### 网络管理单位：Bubble

**Bubble** 是网络管理的最小单位，而非 Entity。每个客户端对应一个 Bubble，Bubble 内包含该客户端相关的所有 Entity。真正的数据同步发生在 **Entity 的 Component** 级别。

```
Server World
  ├─ Bubble[ClientA]  ← 包含 A 相关的 N 个 Entity
  ├─ Bubble[ClientB]  ← 包含 B 相关的 M 个 Entity
  └─ ...
        ↓ 同步
  Component 变更集（增量/全量）
```

**关键设计**：不为海量 Entity 建立独立网络对象，Bubble 负责管理"哪些 Entity 需要同步"，Component 负责"同步什么数据"。这是支撑数万甚至数十万实体的关键。

| 角色 | 职责 |
|------|------|
| **Server (权威端)** | 执行 Gameplay 逻辑，按 Bubble 管理 Entity 可见性，产生权威 Component 状态并广播 |
| **Client (预测端)** | 本地即时执行输入预测，收到服务端 Component 状态后回滚修正（reconciliation） |
| **Host** | 本地既是 Server 也是 Client，无网络延迟的回滚 |

## 编译时宏（DefineConstants）

通过 MSBuild `DefineConstants` 传入，控制编译模式：

| 宏 | 用途 |
|----|------|
| `GP_SERVER` | 专用服务器（Dedicated Server）构建 |
| `GP_WITH_SERVER_CODE` | 包含服务端代码，Host 模式和 Server 模式均定义 |

三种模式的宏组合：

| 模式 | GP_SERVER | GP_WITH_SERVER_CODE |
|------|-----------|---------------------|
| Client | ✗ | ✗ |
| Host | ✗ | ✓ |
| Server | ✓ | ✓ |

```csharp
#if GP_SERVER
    // 仅 Dedicated Server 编译
#endif

#if GP_WITH_SERVER_CODE
    // Host 和 Server 均编译的服务端代码
#endif
```

编译时 `GP_WITH_SERVER_CODE` 包含服务端权威逻辑与 Bubble 管理；`GP_SERVER` 剔除客户端预测/渲染相关代码。GAS 中 `GP_SERVER` 剔除 GameplayCue 等纯客户端表现逻辑。

## 运行时模式判断

网络模式由 World 提供（非 Actor，ECS 中无 Actor 概念），通过 `World.GetNetMode()` 返回 `NetMode` 枚举：

```csharp
public enum NetMode
{
    Standalone,      // 单机（无网络）
    Client,          // 客户端
    DedicatedServer, // 专用服务器
    ListenServer     // 监听服务器 (Host)
}
```

编译时宏决定代码是否编译进程序集，运行时 `GetNetMode()` 决定逻辑分支。两层判断配合使用。

## 多目标

目标平台 **netstandard2.1** + **net10**。

| TFM | 定位 | 典型使用场景 |
|-----|------|-------------|
| `netstandard2.1` | 最大兼容 | Unity、Godot 等引擎集成 |
| `net10` | 最新 API | 独立 Server/Client 进程、AOT 发布 |

代码中通过 TFM 条件编译区分 API：

```csharp
#if NET
    // net10 专有 API
#endif

#if NETSTANDARD2_1
    // netstandard2.1 回退实现
#endif
```

Gameplay.dll 同时产出两个 TFM 的程序集，使用者按目标平台选择引用。
