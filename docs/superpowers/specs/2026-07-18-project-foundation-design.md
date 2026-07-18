# 项目基础 & 最小垂直切片 设计文档

日期：2026-07-18

## 目标

搭建 Gameplay.NET 的工程骨架并完成最小垂直切片验证，确保 ECS 框架在双 TFM（`netstandard2.1` + `net10`）上正常编译和运行。

## 1. 工程骨架

### Gameplay.csproj

```xml
<TargetFrameworks>netstandard2.1;net10</TargetFrameworks>
```

PackageReference：
- `Friflo.Engine.ECS`（ECS 框架）

### Gameplay.Tests.csproj

```xml
<TargetFramework>net10</TargetFramework>
```

PackageReference：
- `xunit`
- `Microsoft.NET.Test.Sdk`
- `xunit.runner.visualstudio`

ProjectReference：
- `../src/Gameplay/Gameplay.csproj`

### Gameplay.NET.slnx

已存在，关联上述两个项目即可。

### 编译验证

```bash
dotnet build src/Gameplay/Gameplay.csproj -f netstandard2.1
dotnet build src/Gameplay/Gameplay.csproj -f net10
dotnet test tests/Gameplay.Tests/Gameplay.Tests.csproj -f net10
```

## 2. NetMode 枚举

文件：`src/Gameplay/NetMode.cs`

```csharp
namespace Gameplay;

public enum NetMode
{
    Standalone,      // 单机（无网络）
    Client,          // 客户端
    DedicatedServer, // 专用服务器
    ListenServer     // 监听服务器 (Host)
}
```

编译宏（`DefineConstants`）在构建时传入，运行时通过 `World.GetNetMode()` 区分逻辑分支。

## 3. World 类（第一版）

文件：`src/Gameplay/World.cs`

```csharp
namespace Gameplay;

public class World
{
    private readonly EntityStore _store;

    public NetMode NetMode { get; }

    public World(NetMode netMode)
    {
        NetMode = netMode;
        _store = new EntityStore();
    }

    public NetMode GetNetMode() => NetMode;
    public EntityStore Store => _store;
}
```

**设计决策**：
- 构造函数接受 `NetMode`，生命周期由上层管理
- `EntityStore` 直接暴露为 `Store` 属性——第一版不做封装，Consumer 直接使用 Friflo API
- 封装层在后续有足够 Consumer 案例后再抽象

## 4. HealthComponent & 测试（最小垂直切片）

### HealthComponent

文件：`src/Gameplay/HealthComponent.cs`

```csharp
namespace Gameplay;

public struct HealthComponent : IComponent
{
    public float Value;
}
```

纯数据组件，行为由 System 定义（后续步骤）。

### 测试

文件：`tests/Gameplay.Tests/HealthComponentTests.cs`

```csharp
namespace Gameplay.Tests;

public class HealthComponentTests
{
    [Fact]
    public void HealthComponent_CanBeAddedAndModified()
    {
        var world = new World(NetMode.Standalone);
        var entity = world.Store.CreateEntity();

        entity.AddComponent(new HealthComponent { Value = 100f });
        ref var health = ref entity.GetComponent<HealthComponent>();
        Assert.Equal(100f, health.Value);

        health.Value -= 30f;
        Assert.Equal(70f, health.Value);
    }
}
```

**验证链路**：World → Store → Entity → AddComponent → GetComponent → 修改 → 断言。

## 5. 验证标准

- [ ] `dotnet build` 两个 TFM 全部通过
- [ ] `dotnet test` 全部通过
- [ ] 文件范围命名空间（`namespace Gameplay;`）用于所有文件
- [ ] 文档和注释使用中文
