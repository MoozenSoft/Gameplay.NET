# 项目基础 & 最小垂直切片 实现计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 搭建 Gameplay.NET 工程骨架（双 TFM csproj + slnx + NetMode + World），并通过 HealthComponent 的最小垂直切片验证 ECS 框架正常工作。

**Architecture:** 4 个源文件 + 1 个测试文件。World 直接暴露 Friflo 的 `EntityStore`，不建封装层。NetMode 是纯枚举。HealthComponent 是纯数据 struct。

**Tech Stack:** .NET (netstandard2.1 + net10), Friflo.Engine.ECS, xUnit

## Global Constraints

- 目标 TFM：`netstandard2.1;net10`（Gameplay.dll）, `net10`（测试项目）
- 文件范围命名空间（`namespace Gameplay;`，不用大括号缩进）
- 文档和注释使用中文
- Friflo.Engine.ECS 通过 NuGet 引用
- 测试框架：xUnit

---

### Task 1: 解决方案 & Gameplay 项目文件

**Files:**
- Create: `src/Gameplay/Gameplay.csproj`
- Create: `Gameplay.NET.slnx`

**Interfaces:**
- Produces: `Gameplay.csproj`（`<TargetFrameworks>netstandard2.1;net10</TargetFrameworks>`，引用 `Friflo.Engine.ECS`）

- [ ] **Step 1: 创建 Gameplay.csproj**

写入 `src/Gameplay/Gameplay.csproj`：

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFrameworks>netstandard2.1;net10</TargetFrameworks>
    <RootNamespace>Gameplay</RootNamespace>
    <AssemblyName>Gameplay</AssemblyName>
    <ImplicitUsings>disable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Friflo.Engine.ECS" Version="3.*" />
  </ItemGroup>

</Project>
```

- [ ] **Step 2: Restore 包并验证**

```bash
dotnet restore src/Gameplay/Gameplay.csproj
```

预期：`Restore succeeded`。

- [ ] **Step 3: 创建解决方案文件**

写入 `Gameplay.NET.slnx`：

```xml
<Solution>
  <Project Path="src/Gameplay/Gameplay.csproj" />
</Solution>
```

- [ ] **Step 4: 验证解决方案构建**

先只构建 net10（快），然后构建 netstandard2.1：

```bash
dotnet build Gameplay.NET.slnx -f net10
dotnet build Gameplay.NET.slnx -f netstandard2.1
```

两个 TFM 均预期：`Build succeeded.`（0 Warning, 0 Error）

- [ ] **Step 5: 提交**

```bash
git add src/Gameplay/Gameplay.csproj Gameplay.NET.slnx
git commit -m "feat: 添加 Gameplay.csproj 和解决方案文件

- 双 TFM: netstandard2.1 + net10
- 引用 Friflo.Engine.ECS
- Co-Authored-By: Claude <noreply@anthropic.com>"
```

---

### Task 2: 测试项目

**Files:**
- Create: `tests/Gameplay.Tests/Gameplay.Tests.csproj`

**Interfaces:**
- Consumes: `src/Gameplay/Gameplay.csproj`（ProjectReference）
- Produces: `Gameplay.Tests.csproj`（`net10`，引用 xUnit + Gameplay 项目）

- [ ] **Step 1: 创建 Gameplay.Tests.csproj**

写入 `tests/Gameplay.Tests/Gameplay.Tests.csproj`：

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10</TargetFramework>
    <RootNamespace>Gameplay.Tests</RootNamespace>
    <AssemblyName>Gameplay.Tests</AssemblyName>
    <ImplicitUsings>disable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.*" />
    <PackageReference Include="xunit" Version="2.*" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.*" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="../../src/Gameplay/Gameplay.csproj" />
  </ItemGroup>

</Project>
```

- [ ] **Step 2: 更新解决方案文件**

编辑 `Gameplay.NET.slnx`，加入测试项目：

```xml
<Solution>
  <Project Path="src/Gameplay/Gameplay.csproj" />
  <Project Path="tests/Gameplay.Tests/Gameplay.Tests.csproj" />
</Solution>
```

- [ ] **Step 3: Restore 并验证空测试项目可构建**

```bash
dotnet restore Gameplay.NET.slnx
dotnet build Gameplay.NET.slnx
```

预期：`Build succeeded.`（0 Warning, 0 Error），注意两个项目都编译。

- [ ] **Step 4: 运行空测试套件确认框架正常**

```bash
dotnet test Gameplay.NET.slnx
```

预期：`0 tests run`，无错误。xUnit runner 正常工作。

- [ ] **Step 5: 提交**

```bash
git add tests/Gameplay.Tests/Gameplay.Tests.csproj Gameplay.NET.slnx
git commit -m "feat: 添加测试项目 Gameplay.Tests

- net10 xUnit 测试项目
- Co-Authored-By: Claude <noreply@anthropic.com>"
```

---

### Task 3: NetMode 枚举 + World 类

**Files:**
- Create: `src/Gameplay/NetMode.cs`
- Create: `src/Gameplay/World.cs`

**Interfaces:**
- Produces: `NetMode` 枚举（`Standalone`, `Client`, `DedicatedServer`, `ListenServer`）
- Produces: `World` 类（构造函数 `World(NetMode netMode)`，属性 `NetMode` / `Store`，方法 `GetNetMode()`）

- [ ] **Step 1: 创建 NetMode.cs**

写入 `src/Gameplay/NetMode.cs`：

```csharp
namespace Gameplay;

/// <summary>
/// 网络运行模式。
/// </summary>
public enum NetMode
{
    /// <summary>单机模式（无网络）。</summary>
    Standalone,

    /// <summary>客户端模式。</summary>
    Client,

    /// <summary>专用服务器模式。</summary>
    DedicatedServer,

    /// <summary>监听服务器模式（Host）。</summary>
    ListenServer,
}
```

- [ ] **Step 2: 创建 World.cs**

写入 `src/Gameplay/World.cs`：

```csharp
using Friflo.Engine.ECS;

namespace Gameplay;

/// <summary>
/// 游戏世界，持有 ECS EntityStore 和网络模式信息。
/// </summary>
public class World
{
    private readonly EntityStore _store;

    /// <summary>当前网络模式。</summary>
    public NetMode NetMode { get; }

    /// <summary>
    /// 创建指定网络模式下的游戏世界。
    /// </summary>
    public World(NetMode netMode)
    {
        NetMode = netMode;
        _store = new EntityStore();
    }

    /// <summary>返回当前网络模式。</summary>
    public NetMode GetNetMode() => NetMode;

    /// <summary>Friflo ECS 实体存储。第一版直接暴露，后续封装。</summary>
    public EntityStore Store => _store;
}
```

- [ ] **Step 3: 构建验证双 TFM**

```bash
dotnet build Gameplay.NET.slnx -f net10
dotnet build Gameplay.NET.slnx -f netstandard2.1
```

预期：两个 TFM 均 `Build succeeded.`（0 Warning, 0 Error）。

- [ ] **Step 4: 提交**

```bash
git add src/Gameplay/NetMode.cs src/Gameplay/World.cs
git commit -m "feat: 添加 NetMode 枚举和 World 类

- NetMode: Standalone / Client / DedicatedServer / ListenServer
- World: 持有 EntityStore，暴露 NetMode 和 Store
- Co-Authored-By: Claude <noreply@anthropic.com>"
```

---

### Task 4: HealthComponent — TDD 最小垂直切片

**Files:**
- Create: `tests/Gameplay.Tests/HealthComponentTests.cs`
- Create: `src/Gameplay/HealthComponent.cs`

**Interfaces:**
- Consumes: `World` 类（`World(NetMode)`, `.Store`, `.GetNetMode()`）, `NetMode.Standalone`
- Produces: `HealthComponent : IComponent { float Value; }`

- [ ] **Step 1: 编写失败测试**

写入 `tests/Gameplay.Tests/HealthComponentTests.cs`：

```csharp
using Xunit;

namespace Gameplay.Tests;

public class HealthComponentTests
{
    [Fact]
    public void CreateEntity_AddHealthComponent_CanReadAndModify()
    {
        // 创建 Standalone World
        var world = new World(NetMode.Standalone);
        Assert.Equal(NetMode.Standalone, world.GetNetMode());

        // 创建 Entity 并添加 HealthComponent
        var entity = world.Store.CreateEntity();
        entity.AddComponent(new HealthComponent { Value = 100f });

        // 读取并验证初始值
        ref var health = ref entity.GetComponent<HealthComponent>();
        Assert.Equal(100f, health.Value);

        // 修改值（模拟伤害）
        health.Value -= 30f;
        Assert.Equal(70f, health.Value);
    }
}
```

- [ ] **Step 2: 运行测试——预期编译失败**

```bash
dotnet test Gameplay.NET.slnx
```

预期：编译错误 `CS0246: The type or namespace name 'HealthComponent' could not be found`。

- [ ] **Step 3: 实现 HealthComponent**

写入 `src/Gameplay/HealthComponent.cs`：

```csharp
using Friflo.Engine.ECS;

namespace Gameplay;

/// <summary>
/// 生命值组件。纯数据，行为由 System 定义。
/// </summary>
public struct HealthComponent : IComponent
{
    /// <summary>当前生命值。</summary>
    public float Value;
}
```

- [ ] **Step 4: 运行测试——预期通过**

```bash
dotnet test Gameplay.NET.slnx
```

预期：`1 passed, 0 failed, 0 skipped`。

- [ ] **Step 5: 最终验证双 TFM 构建**

```bash
dotnet build Gameplay.NET.slnx -f net10
dotnet build Gameplay.NET.slnx -f netstandard2.1
```

预期：全部 `Build succeeded.`。

- [ ] **Step 6: 提交**

```bash
git add tests/Gameplay.Tests/HealthComponentTests.cs src/Gameplay/HealthComponent.cs
git commit -m "feat: 添加 HealthComponent 及测试

- HealthComponent: Friflo IComponent，存 float Value
- 测试: World → Entity → AddComponent → 修改 → 断言
- 验证 ECS 框架在双 TFM 下正常工作
- Co-Authored-By: Claude <noreply@anthropic.com>"
```
