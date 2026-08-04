# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## 项目概述

Gameplay.NET —— 专注游戏玩法的 .NET 类库。目标平台 **netstandard2.1** + **net10.0**，产出 `Gameplay.dll`。

三种 Target 模式：**Client**（客户端）、**Server**（Dedicated Server）、**Host**（Listen Server）。

核心架构：**ECS + GAS + 状态同步（Bubble/预测回滚）**，详见 `.claude/architecture.md`。

## 项目结构

```
Gameplay.NET.slnx   → 解决方案文件，关联所有项目
src/
  Gameplay/              → Gameplay.dll         核心玩法类库
tests/
  Gameplay.Tests/        → Gameplay.Tests.dll    xUnit 单元测试
samples/
  Gameplay.Infrastructure/ → Gameplay.Infrastructure.dll  共享基础设施（网络层、日志等）
  Gameplay.RPG/            → Gameplay.RPG.dll             使用 Gameplay.dll 的玩法示例
  Gameplay.Client/         → Gameplay.Client.exe          客户端入口
  Gameplay.Server/         → Gameplay.Server.exe          Dedicated Server 入口
  Gameplay.Host/           → Gameplay.Host.exe            Listen Server 入口
```

### 依赖关系

```
Gameplay.{Client,Server,Host}.exe
       ↓                        ↓
Gameplay.Infrastructure.dll  Gameplay.RPG.dll
       ↓                              ↓
       └──────────────────────────────┘
              ↓
       Gameplay.dll
```

- **Gameplay.dll**：核心玩法逻辑，不依赖其他项目
- **Gameplay.Infrastructure.dll**：共享基础设施（网络传输、序列化、日志、配置），依赖 Gameplay.dll
- **Gameplay.RPG.dll**：玩法示例，依赖 Gameplay.dll
- **Gameplay.{Client,Server,Host}.exe**：各模式入口，引用 Infrastructure + RPG + Gameplay

## 编译配置

| 宏 | 模式 |
|----|------|
| 无宏 | Client |
| `GP_WITH_SERVER_CODE` | Host |
| `GP_SERVER;GP_WITH_SERVER_CODE` | Server |

运行时通过 `World.GetNetMode()` 返回 `NetMode` 枚举区分模式。详见 `.claude/architecture.md`。

多目标 `netstandard2.1` + `net10.0`，详见 `.claude/architecture.md`。

## 构建命令

```bash
# 构建整个解决方案
dotnet build Gameplay.NET.slnx

# 构建单个项目
dotnet build src/Gameplay/Gameplay.csproj
dotnet build samples/Gameplay.Infrastructure/Gameplay.Infrastructure.csproj

# Release 构建
dotnet build src/Gameplay/Gameplay.csproj -c Release

# 按模式构建（传入宏）
dotnet build src/Gameplay/Gameplay.csproj -p:DefineConstants=GP_WITH_SERVER_CODE          # Host
dotnet build src/Gameplay/Gameplay.csproj -p:DefineConstants=GP_SERVER;GP_WITH_SERVER_CODE # Server

# 指定 TFM
dotnet build src/Gameplay/Gameplay.csproj -f netstandard2.1
```

## 测试命令

```bash
dotnet test Gameplay.NET.slnx
dotnet test tests/Gameplay.Tests/Gameplay.Tests.csproj
dotnet test tests/Gameplay.Tests/Gameplay.Tests.csproj --filter "FullyQualifiedName~ClassName"
dotnet test tests/Gameplay.Tests/Gameplay.Tests.csproj -f net10.0
```

## 示例运行

```bash
dotnet run --project samples/Gameplay.Client/Gameplay.Client.csproj
dotnet run --project samples/Gameplay.Server/Gameplay.Server.csproj
dotnet run --project samples/Gameplay.Host/Gameplay.Host.csproj
```

## 编码约定

- 文档和注释使用**中文**，专业术语使用英文
- C# 命名遵循 .NET 惯例（PascalCase 公开成员，camelCase 私有成员）
- 枚举以 `E` 打头
- **Friflo IComponent 修改必须走 ref**——`TryGetComponent<T>(out var x)` 返回的是栈上拷贝，修改后不写回则静默丢失。修改 Component 的标准模式：
  ```csharp
  // ✅ 正确
  if (entity.HasComponent<T>()) {
      ref var comp = ref entity.GetComponent<T>();
      comp.Field = newValue;
  }
  
  // ❌ 禁止：TryGetComponent(out var) + 修改（值拷贝陷阱）
  ```（如 `EGameplayModOp`、`EEffectEndType`、`EActivationSource`）
- 使用文件范围的命名空间（比如：src/Gameplay/ 目录使用 `namespace Gameplay;`，src/Gameplay/Gameplay.Tags 目录使用 `namespace Gameplay.Tags;`，src/Gameplay/Gameplay.Abilities 目录使用 `namespace Gameplay.Abilities;`，src/Gameplay/Gameplay.Tasks 目录使用 `namespace Gameplay.Tasks;`，tests/Gameplay.Tests/Gameplay.Tests.Tags 目录使用 `namespace Gameplay.Tests.Tags;`，不加大括号缩进）
- **数据驱动（Data-Driven）**：行为由数据定义，逻辑与数据分离。两层含义：① ECS 层——Entity 行为由挂载的 Component 组合决定，不靠类继承；Component 为纯数据 struct（无行为方法），System 持有全部逻辑。② 配置层——玩法数值（血量、伤害、冷却时间等）不进代码，走静态数据配置（如 JSON、数据表）；代码只定义数据结构和消费方式，具体值由策划/配置决定。判断"能不能做某事" = 查数据而非写死条件。
- **事件驱动（Event-Driven）**：跨系统通信走事件总线，避免直接耦合。生产方 `GameplayEventBus.Enqueue(in record)` —— 写进去就不管谁来消费；消费方注册 `IGameplayEventHandler` 或动态 Listener，由 `GameplayEventDispatcher.Tick()` 统一分发。适合伤害/治疗/拾取等需要多方响应的场景，添加新消费者无需改动生产者。
- **命令模式（Command Pattern）**：将请求/操作封装为接口对象，统一执行流程。如 `IAbilityRequirement.Evaluate()`（条件检查，纯函数无副作用）、`IAbilityCommit.Execute()`（副作用提交，可回滚）、`IAbilityExecutor.Execute()`（执行体）、`IGameplayEventHandler.Handle()`（事件处理）。接口约束让同类操作可串联组合、入队延迟执行、失败时回滚。
- **依赖方向（允许的耦合 vs 不允许的耦合）**：区分"能力构建在服务之上"（允许）与"平台依赖业务规则"（不允许）。
  - ✅ **允许：消费**——能力 System 可引用其他域的服务/数据/类型来完成自己的能力，依赖方向单向（能力是消费者，服务是提供者）。例：`GameplayEventSystem` 引用 `GameplayEventDispatcher`、`AttributeListenerSystem` 引用 `AttributeAggregatorManager`、`CommitPhaseListenerSystem` 读取 `ActiveAbilityComponent`、`TaskBuilder` 的 `GameplayAttribute` 参数。耦合是能力定义的一部分——去掉耦合等于去掉能力。
  - ❌ **禁止：倒置**——通用平台层（Runtime / 基础设施）不得依赖上层业务对象或编码业务规则。例：`TaskSchedulerSystem` 不得直接持有 `AbilityActivationManager`（"Task 全完成 → CancelAbility"是 GAS 决策）；必须通过输出协议（如 `ITaskCompletionListener`）把决策权留给上层实现。判据：平台是否开始替上层做业务决定。
- 遵守 TDD（测试驱动开发）
- 优先使用 0 GC 方案，但酌情权衡——热路径（每帧遍历大量 Entity 的 System）严格要求；冷路径（初始化、配置加载、RPC 处理）可放松，以可读性为先：
  - **struct 代替 class**：值类型栈分配，无 GC 压力；ECS Component 均为 struct
  - **`ref struct` / `ref` 返回 / `ref` 字段（C# 11）**：防止值类型逃逸到堆；`ref struct` 内可存 ref 字段
  - **`scoped ref`（C# 11）**：约束 ref 生命周期，避免编译器保守的逃逸分析
  - **`in` 参数修饰符**：按只读引用传递大 struct，避免拷贝开销
  - **`ref readonly` 返回**：返回只读引用，避免拷贝
  - **`Span<T>` / `ReadOnlySpan<T>` / `Memory<T>`**：栈上安全视图，零分配切片
  - **`stackalloc`**：栈上分配临时缓冲区（`Span<int> buf = stackalloc int[64]`）
  - **`[SkipLocalsInit]`**：跳过 `stackalloc` 零初始化（已知写入全部元素时用）
  - **`InlineArray`（C# 12）**：`[InlineArray(16)]` 在 struct 内嵌入固定长度数组，不分配堆内存
  - **`fixed` 字段**：`fixed int buffer[64]` 在 struct 内直接嵌入，无需单独数组对象
  - **`params Span<T>`（C# 13 / net10.0）**：params 传参不分配数组
  - **ArrayPool / ObjectPool**：复用临时数组和对象，`Rent` → `Return`；`finally` 块归还
  - **`GC.AllocateUninitializedArray<T>()`**：跳过数组零初始化（你立即写入全部元素时用）
  - **`Array.Empty<T>()`**：共享空数组单例，代替 `new T[0]`
  - **`CollectionsMarshal.AsSpan<T>()`**：从 `List<T>` 获取内部 `Span`，零分配访问
  - **`SearchValues<T>`**：预计算搜索模式，零分配字符串/字节查找
  - **`ValueStringBuilder`**：栈上字符串拼接，不分配 `StringBuilder`（可参考 .NET 内部实现或自定义）
  - **`ValueTask<T>` / `ValueTask`**：避免同步完成路径的 `Task` 分配；ECS 中 async 很少用，但 RPC 层适用
  - **`StringBuilderCache` / `StringPool`**：缓存并复用 `StringBuilder` 实例
  - **`[ThreadStatic]` / `ThreadLocal<T>`**：每线程复用缓冲区，无锁无分配
  - **`static` 匿名函数**：`static () => ...` 禁止闭包捕获，杜绝隐式委托分配
  - **手动 struct 枚举器**：实现 `Current` / `MoveNext()` / `GetEnumerator()` 为 struct 类型，`foreach` 不装箱
  - **枚举 / bit flags 代替 `string`**：用 `enum` 或 `[Flags]` 做标识/标签，避免字符串比较和分配
  - **`IEquatable<T>` / `IComparable<T>`**：泛型接口避免值类型装箱
  - **避免 LINQ 在热路径**：`foreach` 遍历 `List<T>`（非 `IEnumerable<T>`）不分配枚举器
  - **避免闭包 / 缓存委托**：不在循环中 `() => ...` 或 `new EventHandler(...)`
  - **`[MethodImpl(AggressiveInlining)]`**：热路径小方法内联，减少调用开销

<!-- CODEGRAPH_START -->
## CodeGraph

In repositories indexed by CodeGraph (a `.codegraph/` directory exists at the repo root), reach for it BEFORE grep/find or reading files when you need to understand or locate code:

- **MCP tool** (when available): `codegraph_explore` answers most code questions in one call — the relevant symbols' verbatim source plus the call paths between them, including dynamic-dispatch hops grep can't follow. Name a file or symbol in the query to read its current line-numbered source. If it's listed but deferred, load it by name via tool search.
- **Shell** (always works): `codegraph explore "<symbol names or question>"` prints the same output.

If there is no `.codegraph/` directory, skip CodeGraph entirely — indexing is the user's decision.
<!-- CODEGRAPH_END -->
