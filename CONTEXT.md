# Gameplay.NET

游戏玩法类库，基于 ECS + GAS + 状态同步，产出 `Gameplay.dll`。

## Language

**GameplayTag**:
一组层级化的标签标识符，用点分隔（如 `"Damage.Fire"`）。用于分类实体、匹配条件、过滤查询。注册时分配全局唯一 int id。
_Avoid_: Tag, label, flag

**GameplayTags**:
附加到 Entity 上的 `IComponent`，内部用位集存储该实体拥有的所有 GameplayTag。是 GameplayTag 在 ECS 中的运行时载体。
_Avoid_: TagComponent, TagSet

**层级匹配（Hierarchical Matching）**:
查询子标签时自动匹配所有父标签。例如 Entity 有 `Damage.Fire.DoT`，`Matches(Damage)` 返回 true。通过预计算展开集（自身 + 所有子孙 Tag ID）实现。
_Avoid_: Tag matching, parent matching

**展开集（Expanded Set）**:
每个 GameplayTag 在注册完成后预计算的位集，包含该 Tag 自身及其所有子孙 Tag 的 ID。查询时直接做位与，不用递归。
_Avoid_: Descendant set, child set

**RegisterTags**:
唯一写入 GameplayTag 的入口。接受层级 Tag 名字符串数组，自动创建缺失的父节点。通常在游戏启动时调用一次。
_Avoid_: AddTag, CreateTag

**RequestTag**:
只读查询已注册的 GameplayTag。不存在则返回 `GameplayTag.Invalid`。永不创建新 Tag。
_Avoid_: GetTag, FindTag

**GameplayTask**:
GAS 的异步任务节点，对应 UE5 AbilityTask。每个 Task 是一个 Entity，挂 Component 承载状态和数据，由对应的 System 驱动推进。不以 class + Update() 实现。
_Avoid_: AsyncTask, Coroutine, AbilityNode

**DelayTask**:
GameplayTask 的一个类型——延时等待。`DelayTaskSystem` 每帧累加 `Elapsed`，到达 `Duration` 后标记 `Done`。
_Avoid_: WaitTask, TimerTask

**TaskState**:
Task 的执行阶段：`Pending`（等待开始）→ `Running`（执行中）→ `Done`（完成）或 `Cancelled`（取消）。System 不销毁 Done/Cancelled 的 Entity，由外部决策。
_Avoid_: Status, Phase

**GameplayAbility**:
技能的静态定义（策划配置的数据资产，非 Entity）。激活流程走 `IAbilityRequirement` → `IAbilityCommit` → `IAbilityExecutor` 三接口。
_Avoid_: AbilityDefinition, Skill

**AbilitySpec**:
能力授予到具体 Entity（Owner）的实例数据快照（Cost/CD/Level 等），`TryActivate` 的前置句柄。
_Avoid_: GrantedAbility, AbilityEntry

**ActiveAbilityComponent**:
激活中能力实例的运行时标记 Component——一次激活 = 一个挂此组件的 Entity（Owner 子 Entity）。
_Avoid_: RunningAbility, ActiveAbilityEntity

**GameplayEffect**:
效果的静态定义（数据资产，非 Entity），描述持续/瞬时效果（Duration/Period/Modifiers/Stacking）。
_Avoid_: Buff, Debuff, EffectDefinition

**ActiveGameplayEffectComponent**:
GE 的运行时 Entity Component（挂在 target 子 Entity），持 Tick 所需的 Duration/Stack/Period 状态。
_Avoid_: EffectInstance, RuntimeEffect

**GameplayAttribute**:
属性寻址句柄（Id + ComponentType + Offset），指向 AttributeSet Component 内的字段，供 Mod 读写。
_Avoid_: Stat, Field

**AttributeAggregator**:
单个属性的修改器桶（Additive/Multiply/Divide/Override/FinalAdd）+ Evaluate 公式，由 `AttributeAggregatorManager` 管理。
_Avoid_: ModifierStack, StatAggregator

**GameplayEvent**:
事件总线消息（record payload），跨系统解耦。生产侧 `Enqueue` 写 pending 帧，消费侧经 Dispatcher 分发到 Handler。
_Avoid_: Message, Notification, Callback

**NetworkId**:
跨进程实体网络身份（服务端分配自增正数，0 = Invalid）。身份在 packet 信封、状态在 payload，不在组件里。
_Avoid_: NetGuid, RemoteId

**Bubble**:
每个客户端「应可见」的 `NetworkId` 集合；进复制集时按 Owner 规则一次决定、不迁移。
_Avoid_: RelevantSet, InterestSet

**Mirrored**:
每个客户端「已发送/客户端已镜像」的 `NetworkId` 集合；`Bubble − Mirrored` 的差集决定下次 spawn。
_Avoid_: Sent, ReplicatedSet

**shadow-diff**:
变更检测——每 World 持上次已发送值拷贝（shadow），逐帧比较产出 dirty 增量，稳态零序列化。
_Avoid_: DirtyCheck, DeltaSnapshot

**镜像实体（Mirror Entity）**:
客户端本地持有的只读副本 Entity，经 `NetworkId` 映射与本地实体区分；客户端是纯镜像 World，不跑模拟。
_Avoid_: Replica, Ghost, Proxy

**ReplicationRegistry**:
复制集注册中心（static 类型级）——装配「`SerializerRegistry` 的序列化器 + SG 生成的 diff」，shadow 状态则是 per-World 实例。
_Avoid_: SyncRegistry, ReplicationSet
