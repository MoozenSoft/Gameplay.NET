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
- 使用文件范围的命名空间（比如：`namespace Gameplay;`，不加大括号缩进）

<!-- CODEGRAPH_START -->
## CodeGraph

In repositories indexed by CodeGraph (a `.codegraph/` directory exists at the repo root), reach for it BEFORE grep/find or reading files when you need to understand or locate code:

- **MCP tool** (when available): `codegraph_explore` answers most code questions in one call — the relevant symbols' verbatim source plus the call paths between them, including dynamic-dispatch hops grep can't follow. Name a file or symbol in the query to read its current line-numbered source. If it's listed but deferred, load it by name via tool search.
- **Shell** (always works): `codegraph explore "<symbol names or question>"` prints the same output.

If there is no `.codegraph/` directory, skip CodeGraph entirely — indexing is the user's decision.
<!-- CODEGRAPH_END -->
