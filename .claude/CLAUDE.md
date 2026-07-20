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
- 使用文件范围的命名空间（比如：src/Gameplay/ 目录使用 `namespace Gameplay;`，src/Gameplay/GameplayTags 目录使用 `namespace Gameplay.GameplayTags;`, samples/Gameplay.Server 目录使用 `namespace Gameplay.Server;`, tests/Gameplay.Tests/GameplayTags 目录使用 `namespace Gameplay.Tests.GameplayTags;` 之后没多一级目录命名空间就多加一级, 不加大括号缩进）
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
