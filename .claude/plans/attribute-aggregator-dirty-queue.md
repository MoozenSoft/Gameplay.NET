# Attribute Aggregator 脏队列重构

## 目标

移除以全局 `AttributeId` 位图驱动的 `DirtyAttributeComponent`，改为由每个 `AttributeAggregator` 自身维护脏状态，并由 POCO `AttributeAggregatorManager` 在固定 Tick 阶段批量刷新。实现目标：

- Attribute / AttributeSet 数量不再受 64-bit `DirtyBits` 限制。
- 一个 `(Entity, GameplayAttribute)` 在一个 Flush 周期内最多 `Evaluate()` 一次。
- 所有正常 Attribute 读取仅得到上一个固定 Flush 阶段的已结算值，不因首次读取发生的 System 阶段而隐式重算。
- 保持 ActiveGameplayEffect 为 Entity、EffectSystem 为 ECS `QuerySystem`；聚合计算、脏队列和生命周期管理为 POCO。
- 热路径 steady-state 零 GC：复用 Dictionary / List 容量，不在 Flush 内创建临时集合或闭包。

本次不实现 `AttributeChangedEvent`。该事件需要给 `GameplayEventRecord` 配套类型化 payload（Attribute、OldValue、NewValue），单独规划；当前改造仅稳定地写回 `CurrentValue`。

## 已确认的运行时约定

```text
Phase 0  GameplayEventDispatcher.Tick()：消费上一帧事件
Phase 1  Task QuerySystems（含 WaitAttributeChangeTaskSystem）
Phase 2  AbilityTaskSystem
Phase 3  EffectSystem：Apply / Remove / Duration / Period，调用 AttributeAggregatorManager 修改聚合器
Phase 4  AttributeAggregatorManager.Flush()：统一 Evaluate、写回 CurrentValue
Phase 5  AbilityActivationManager.ProcessPendingDeletions()
```

- Phase 1–3 对属性的修改只会标记 aggregator 脏。
- Phase 4 是常规 Gameplay 读取新 `CurrentValue` 的唯一结算边界。
- `GetCurrentValue()` 不隐式 `Evaluate()`：脏时仍返回 Component 的上次 `CurrentValue`。没有 aggregator 时返回 Component 的 `BaseValue`（初始 CurrentValue = BaseValue）。
- 若未来出现必须在 Phase 4 前读取最新值的特殊业务，另行设计显式 `FlushAttribute(entity, attribute)` API；本次不提供绕过固定阶段的隐式路径。
- Flush 中的新修改不能混入正在遍历的队列；使用两个可复用 List 双缓冲，留待下一轮 Flush，避免 listener/后续扩展产生无界同帧递归。
- `WaitAttributeChangeTaskSystem` 留在 Phase 1，它读取的是上次 Flush 后的已结算值。属性变化 → 下一帧 Phase 1 Task 检测到，具有确定性。

## 标识与核心数据

### GameplayAttribute（SG 生成的类型安全字段标识）

`GameplayAttribute` 持有生成的读写委托，`Id` 作稳定 registry key：

```csharp
public readonly struct GameplayAttribute : IEquatable<GameplayAttribute>
{
    public readonly int Id;

    internal readonly TryReadValue TryReadBaseValue;
    internal readonly TryReadValue TryReadCurrentValue;
    internal readonly WriteValue   WriteCurrentValue;

    // 相等性只比较 Id（全局唯一），不比较委托
    public bool Equals(GameplayAttribute other) => Id == other.Id;
    public override int GetHashCode() => Id;

    internal delegate bool TryReadValue(Entity entity, out float value);
    internal delegate void WriteValue(Entity entity, float value);
}
```

SG 生成两层：
1. 静态方法（已有，保留）—— 编译期已知类型时直接使用：
   ```csharp
   public partial struct TestAttrSet {
       public static ref GameplayAttributeData GetHealth(Entity entity)
           => ref entity.GetComponent<TestAttrSet>().Health;
   }
   ```
2. GameplayAttribute 句柄（已有，扩展）—— Manager 通过统一接口调用，类型检查编进委托：
   ```csharp
   public static class TestAttrSetAttributes {
       public static readonly GameplayAttribute Health = new(
           id: 1,
           tryReadBaseValue: entity => {
               ref var set = ref entity.GetComponent<TestAttrSet>();
               return (true, set.Health.BaseValue);
           },
           tryReadCurrentValue: entity => {
               ref var set = ref entity.GetComponent<TestAttrSet>();
               return (true, set.Health.CurrentValue);
           },
           writeCurrentValue: (entity, value) => {
               ref var data = ref entity.GetComponent<TestAttrSet>().Health;
               data.CurrentValue = value;
           }
       );
   }
   ```
   委托为 `static readonly` 一次性分配，零稳态 GC。

SG 按 namespace + struct + field 的稳定排序生成 ID。删除 `id > 63` 的 `#error` 与 DirtyBits 注释。

### GameplayAttributeHandle（数据配置侧的轻量标识符）

```csharp
public readonly struct GameplayAttributeHandle : IEquatable<GameplayAttributeHandle>
{
    public readonly int Id;
    public GameplayAttributeHandle(int id) => Id = id;
    public static implicit operator GameplayAttributeHandle(GameplayAttribute attr) => new(attr.Id);
}
```

`GameplayModifier` 存 `GameplayAttributeHandle`：
- 代码侧：`CombatAttributes.Health` 隐式转换
- 配置/JSON 侧：`new GameplayAttributeHandle(jsonData.attributeId)` 反序列化
- 使用前由 Manager 内部通过 `registeredAttributes[id]` 还原完整 `GameplayAttribute`

### AttributeKey

```csharp
internal readonly struct AttributeKey : IEquatable<AttributeKey>
{
    internal readonly Entity Entity;
    internal readonly GameplayAttributeHandle Attribute;
}
```

以完整 `Entity`（含 `Id` + `Revision` + Store 身份）和 AttributeHandle 标识作为 key。

### AttributeAggregator（受控状态 API）

- `SetBaseValue(float)`：值真实改变时返回 `true`；不直接公开可写字段。
- `AddMod(handle, magnitude, op)`：添加后返回 `true`。
- `RemoveModsByHandle(handle)`：手写双指针就地压缩（零 GC），仅实际移除项时返回 `true`。
- `Evaluate()`：只计算并返回结果——不清 Dirty。Dirty 由 Manager 在写回/队列事务完成后统一管理。
- `Dirty`：**仅由 Manager 读取和修改**，作为"本轮是否已入队"的去重哨兵。

### BaseValue 统一入口

`SetBaseValue(entity, attr, value)` 作为唯一 BaseValue 写入入口：写 Component `BaseValue` + 同步 aggregator `BaseValue` + MarkDirty。禁止 Gameplay 代码直接写 `GameplayAttributeData.BaseValue`。

## 新 POCO：AttributeAggregatorManager

### 数据

```csharp
Dictionary<AttributeKey, AttributeAggregator> aggregators;
Dictionary<int, GameplayAttribute> registeredAttributes;
// 反向索引
Dictionary<Entity, List<AttributeKey>> entityToAttributes;       // Entity→属性清理 O(该Entity的属性数)
Dictionary<int, List<AttributeKey>> handleToAttributes;          // handle→属性 O(该Effect影响的属性数)
// 脏队列
List<AttributeKey> currentDirtyQueue;
List<AttributeKey> nextDirtyQueue;
bool isFlushing;
```

### API

| API | 说明 |
|-----|------|
| `RegisterAttribute(GameplayAttribute)` | 拒绝冲突 ID（`throw InvalidOperationException`） |
| `SetBaseValue(Entity, GameplayAttributeHandle, float)` | 唯一 BaseValue 写入入口 |
| `SetAggregatorValue(Entity, GameplayAttributeHandle, float)` | 创建 aggregator 时从 Component 读取 BaseValue 初始化 |
| `AddAggregatorMod(Entity, GameplayAttributeHandle, int handle, float magnitude, EGameplayModOp op)` | 仅在已有 aggregator 时操作；维护 `handleToAttributes` 反向索引 |
| `RemoveAggregatorModsByHandle(int handle)` | 通过 `handleToAttributes` 反向索引 O(受影响的属性数)，不扫全表 |
| `GetCurrentValue(Entity, GameplayAttributeHandle)` | 返回 Component `CurrentValue`；无 aggregator 时返回 Component `BaseValue` |
| `GetBaseValue(Entity, GameplayAttributeHandle)` | 返回 aggregator BaseValue；不存在时读取 Component 的 BaseValue |
| `HasAggregator(Entity, GameplayAttributeHandle)` | 按 `AttributeKey` 查询 |
| `Flush()` | 遍历 `currentDirtyQueue` → Evaluate → 写回 CurrentValue → `Dirty = false`。无效 Entity/不存在的 aggregator 静默跳过。结算后交换队列。 |
| `RemoveEntity(Entity)` | 通过 `entityToAttributes` 反向索引清理 aggregators + 队列项 |

所有公开 API 的 Attribute 参数接受 `GameplayAttributeHandle`，Manager 内部通过 `registeredAttributes` 解析。

### MarkDirty

```
MarkDirty(key, aggregator):
    if aggregator.Dirty → return
    aggregator.Dirty = true
    if isFlushing → nextDirtyQueue.Add(key)
    else → currentDirtyQueue.Add(key)
```

- 同一 aggregator 同一周期只入队一次，不需要额外 `HashSet`
- 双缓冲确保 Flush 期间的新增 Dirty 留到下一帧，防止无界同帧递归
- 未来 `AttributeChangedEvent` 阶段如有链式属性修改，也走 next 队列

### Evaluate 异常处理

不添加 try-catch。`Evaluate()` 失败是代码/数据 bug，该直接炸出来。

### RemoveAll 委托 GC 消除

`AttributeAggregator.RemoveModsByHandle` 改写为手写双指针就地压缩，消除 `RemoveAll(m => ...)` 的委托闭包分配。

## Entity 生命周期

对 `EffectSystem` 和 `AbilityActivationManager` 这两个 Entity 的所有者，**谁删 Entity 谁负责删前通知**：

```csharp
// EffectSystem.RemoveEffect
manager.RemoveEntity(effectEntity);
effectEntity.DeleteEntity();

// AbilityActivationManager.ProcessPendingDeletions
manager.RemoveEntity(entity);
entity.DeleteEntity();
```

不挂 `EntityStore.OnEntityDelete` 事件，不走 `CommandBuffer` 批量删除。Manager 通过 `entityToAttributes` 反向索引 O(该 Entity 的属性数) 清理 aggregators 和 dirtyQueue。

## EffectSystem 集成变更

1. 构造参数从 `AttributeSystem` 改为 `AttributeAggregatorManager`
2. `Apply` 删除 `HasComponent<DirtyAttributeComponent>` 门卫——直接调 Manager API，缺少对应 AttributeSet 时 SG 委托自然炸出
3. 删除所有 `dirty.SetBit(mod.AttributeId)` 调用——`MarkDirty` 由 Manager 内部处理
4. `RemoveEffect` 调 `manager.RemoveAggregatorModsByHandle(handle)` + `manager.RemoveEntity(entity)`

## Feature 集成

```csharp
public class GameplayAbilitiesFeature
{
    public AttributeAggregatorManager AttributeAggregatorManager { get; }

    public void Update(float deltaTime)
    {
        EventDispatcher.Tick();                    // Phase 0
        SystemRoot.Update(new UpdateTick(...));    // Phase 1-3 (Task + Effect)
        AttributeAggregatorManager.Flush();          // Phase 4
        ActivationManager.ProcessPendingDeletions(); // Phase 5
    }
}
```

`AttributeAggregatorManager` 不再注册到 `SystemRoot`。

## Attribute API 迁移

| 当前 | 改为 |
|------|------|
| `GameplayModifier.AttributeId` (int) | `GameplayModifier.Attribute` (GameplayAttributeHandle) |
| `FModifierSpec.AttributeId` (int) | `FModifierSpec.Attribute` (GameplayAttributeHandle) |
| `WaitAttributeChangeComponent.AttributeId` (int) | `WaitAttributeChangeComponent.Attribute` (GameplayAttributeHandle) |

删除 `DirtyAttributeComponent.cs` 及其测试。

## TDD 实施顺序

1. **写 Manager 与 Aggregator 单元测试**：同一 Attribute 连续多次 Set/Add 在一次 Flush 只结算一次；多 Attribute 独立结算；无 Modifier 的 BaseValue 变更也结算；移除不存在 handle 不污染队列；首次 Aggregator 创建保留组件 BaseValue。
2. **实现 AttributeKey、受控 AttributeAggregator API、AttributeAggregatorManager 的脏队列与双缓冲 Flush**，让测试通过。
3. **写 SG 测试**：第 65 个及之后的 Attribute 正常生成（不再 `#error`）；生成 reader/writer 能安全处理 Entity 缺少指定 Set 的情况；句柄 equality 稳定。
4. **实现 SG 与 `GameplayAttribute` 的 reader/writer API + `GameplayAttributeHandle`**，移除 64 上限。
5. **迁移 Attribute 消费 API 与 GE 数据结构至 `GameplayAttributeHandle`**；更新 `EffectSystem`、Commit、Wait Task 及全部测试 fixtures。
6. **移除 DirtyAttributeComponent 与 QuerySystem 依赖**；将 Manager 置于 `Feature.Update()` 的 Phase 4，验证 Effect Apply/Remove 到 CurrentValue 写回的完整链路。
7. **写生命周期与确定性集成测试**：Entity Delete 触发清理；一个 Tick 多次 Apply/Remove 只结算最终值一次；Flush 中新增脏项留待下一次 Flush；Flush 前 `GetCurrentValue` 返回上次结算值；没有 aggregator 时返回 BaseValue。
8. 运行完整 `dotnet test Gameplay.NET.slnx -f net10.0` 和 `dotnet build Gameplay.NET.slnx`；验证 Client / Host / Server DefineConstants 构建。

## 本次明确不做

- `AttributeChangedEvent` / `GameplayEventRecord` 类型化 payload 改造。
- RealTime capture 反向索引：删除现有 `realTimeReverseIndex` 死代码；真正实现时再单独设计。
- `GameplayEffect` 中既有临时 List、Random、Modifier bucket 容量等其他 GC 问题的全面整改；只确保新增 Manager/Flush 热路径不产生稳态 GC。
