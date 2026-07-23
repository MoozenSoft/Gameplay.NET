# GameplayAbilities Plan 4: Source Generator

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 实现 `[GameplayAttribute]` 和 `[GameplayEvent]` Source Generator，编译期生成类型安全访问器和注册表

**Architecture:** C# Roslyn Incremental Source Generator，生成 `Gameplay.CodeGen` NuGet 包。扫描用户 Attribute → 生成 partial class/struct 追加到用户代码。

**Tech Stack:** C# (.NET 10 / netstandard2.1 + SG project netstandard2.0), Microsoft.CodeAnalysis.CSharp, xUnit

**Spec:** `docs/superpowers/specs/2026-07-21-gameplay-abilities-design.md`

---

## Global Constraints

- 枚举以 `E` 打头
- 命名空间：SG 项目 `Gameplay.CodeGen`，生成代码并入 `Gameplay.Abilities`
- TDD：写测试 → 失败 → 实现 → 通过 → 提交
- 跨 TFM: `netstandard2.0`（SG 项目必须）+ `net10.0`（测试引用）

## 文件结构

```
src/Gameplay.CodeGen/
├── Gameplay.CodeGen.csproj         # SG 项目，TargetFramework=netstandard2.0
├── GameplayAttributeAttribute.cs   # [GameplayAttribute] 标记 Attribute
├── GameplayEventAttribute.cs       # [GameplayEvent] 标记 Attribute
├── GameplayAttributeGenerator.cs   # 扫描 [GameplayAttribute] → 生成访问器
└── GameplayEventGenerator.cs       # 扫描 [GameplayEvent] → 生成 ID/Registry

src/Gameplay/Gameplay.csproj        # 引用 SG: <ProjectReference OutputItemType="Analyzer" />
src/Gameplay/Gameplay.Abilities/
├── Attribute/
│   └── Generated/                   # SG 产出（不提交到 git）
└── GameplayEvent/
    └── Generated/                   # SG 产出（不提交到 git）

tests/Gameplay.Tests/
├── Gameplay.Tests.csproj            # 引用 SG 同上
└── Gameplay.Tests.Abilities/
    ├── Attribute/
    │   └── AttributeSGTests.cs      # 生成代码的编译期测试
    └── GameplayEvent/
        └── GameplayEventSGTests.cs
```

---

### Task 1: Source Generator 项目骨架

**Files:**
- Create: `src/Gameplay.CodeGen/Gameplay.CodeGen.csproj`
- Create: `src/Gameplay.CodeGen/GameplayAttributeAttribute.cs`
- Create: `src/Gameplay.CodeGen/GameplayEventAttribute.cs`
- Modify: `src/Gameplay/Gameplay.csproj`（添加 SG 引用）

- [ ] **Step 1: 创建 SG 项目**

```xml
<!-- src/Gameplay.CodeGen/Gameplay.CodeGen.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>netstandard2.0</TargetFramework>
    <EnforceExtendedAnalyzerRules>true</EnforceExtendedAnalyzerRules>
    <IsRoslynComponent>true</IsRoslynComponent>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.CodeAnalysis.CSharp" Version="4.*" PrivateAssets="all" />
    <PackageReference Include="Microsoft.CodeAnalysis.Analyzers" Version="3.*" PrivateAssets="all" />
  </ItemGroup>
</Project>
```

- [ ] **Step 2: 创建标记 Attribute**

```csharp
// GameplayAttributeAttribute.cs
namespace Gameplay.Abilities;

/// <summary>标记 AttributeSet struct 中的字段为 GameplayAttribute。SG 扫描生成访问器。</summary>
[AttributeUsage(AttributeTargets.Field)]
public class GameplayAttributeAttribute : System.Attribute { }
```

```csharp
// GameplayEventAttribute.cs
namespace Gameplay.Abilities;

/// <summary>标记 struct 为 GameplayEvent。SG 扫描生成 EventId + Registry。</summary>
[AttributeUsage(AttributeTargets.Struct)]
public class GameplayEventAttribute : System.Attribute
{
    public string Tag { get; init; }
}
```

- [ ] **Step 3: 在 Gameplay.csproj 中引用 SG + 在 Gameplay.Tests.csproj 中引用 SG**

```xml
<ItemGroup>
  <ProjectReference Include="../../src/Gameplay.CodeGen/Gameplay.CodeGen.csproj"
                    OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
</ItemGroup>
```

- [ ] **Step 4: 编译验证 SG 项目 + 主项目能正常构建**

```bash
dotnet build src/Gameplay.CodeGen/Gameplay.CodeGen.csproj
dotnet build src/Gameplay/Gameplay.csproj -f net10.0
```

- [ ] **Step 5: 提交**

```bash
git add src/Gameplay.CodeGen/ src/Gameplay/Gameplay.csproj tests/Gameplay.Tests/Gameplay.Tests.csproj
git commit -m "feat: add Source Generator project skeleton with GameplayAttribute and GameplayEvent attributes

Co-Authored-By: Claude <noreply@anthropic.com>"
```

---

### Task 2: GameplayAttributeGenerator

**Files:**
- Create: `src/Gameplay.CodeGen/GameplayAttributeGenerator.cs`
- Create: `tests/Gameplay.Tests/Gameplay.Tests.Abilities/Attribute/AttributeSGTests.cs`

**Interfaces:**
- Consumes: `[GameplayAttribute]` on IAttributeSetComponent fields
- Produces: 每个标记字段生成 `Get{Name}(Entity)` 静态方法

**SG 逻辑：**
1. 扫描所有实现了 `IAttributeSetComponent` 的 struct
2. 找到其中标记了 `[GameplayAttribute]` 的字段
3. 生成强类型访问器：

```csharp
// 用户代码:
public struct CombatAttributeSet : IAttributeSetComponent
{
    [GameplayAttribute] public GameplayAttributeData Health;
    [GameplayAttribute] public GameplayAttributeData MaxHealth;
}

// SG 生成（partial）:
public partial struct CombatAttributeSet
{
    public static ref GameplayAttributeData GetHealth(Entity entity)
        => ref entity.GetComponent<CombatAttributeSet>().Health;
    public static ref GameplayAttributeData GetMaxHealth(Entity entity)
        => ref entity.GetComponent<CombatAttributeSet>().MaxHealth;
}
```

- [ ] **Step 1: 写测试 — 定义 IAttributeSetComponent + [GameplayAttribute]，验证生成代码可编译并访问**

```csharp
// 测试通过定义 struct + 编译 + 实例化 + 调用生成的 GetXxx 方法
[Fact]
public void GeneratedAccessor_GetsCorrectField()
{
    var store = new EntityStore();
    var entity = store.CreateEntity();
    entity.AddComponent(new TestAttributeSet { Health = new() { BaseValue = 100f } });
    
    // 这个方法由 SG 生成
    ref var health = ref TestAttributeSet.GetHealth(entity);
    Assert.Equal(100f, health.BaseValue, 0.001f);
}

// 定义在测试项目中 — SG 应扫描到
public struct TestAttributeSet : IAttributeSetComponent
{
    [GameplayAttribute] public GameplayAttributeData Health;
}
```

- [ ] **Step 2-5: 实现 SG → 运行测试 → 提交**

---

### Task 3: GameplayEventGenerator

**Files:**
- Create: `src/Gameplay.CodeGen/GameplayEventGenerator.cs`
- Create: `tests/Gameplay.Tests/Gameplay.Tests.Abilities/GameplayEvent/GameplayEventSGTests.cs`

**SG 逻辑：**
1. 扫描所有标记了 `[GameplayEvent]` 的 struct
2. 生成 `GameplayEventId` enum + `GameplayEventRegistry`

```csharp
// 用户代码:
[GameplayEvent(Tag = "Event.Damage")]
public partial struct DamageEvent
{
    public float Amount;
    public Entity Source;
}

// SG 生成:
public enum EGameplayEventKind : ushort
{
    Damage = 1,
}

public static class GameplayEventRegistry
{
    public static readonly System.Collections.Generic.Dictionary<ushort, string> Tags = new()
    {
        [1] = "Event.Damage",
    };
}
```

- [ ] **Step 1: 写测试**
- [ ] **Step 2-5: TDD + commit**

---

### Task 4: 集成 — 用 GameplayAttribute 替换裸 int AttributeId

**Files:**
- Modify: `src/Gameplay/Gameplay.Abilities/GameplayEffect/GameplayModifier.cs` — `AttributeId` 改为可同时接受 `GameplayAttribute` 和 `int`
- 添加示例 AttributeSet 到测试项目验证端到端流程

**Interfaces:**
- Consumes: GameplayAttributeGenerator (Task 2)
- Produces: GameplayAttribute struct（SG 生成）替代裸 int

- [ ] **Step 1: 写端到端测试 — 创建 AttributeSet → 创建 GE → Apply → 验证 CurrentValue**
- [ ] **Step 2-5: TDD + commit**

---

## 验证

```bash
dotnet build src/Gameplay.CodeGen/Gameplay.CodeGen.csproj
dotnet build src/Gameplay/Gameplay.csproj -f net10.0
dotnet test tests/Gameplay.Tests/Gameplay.Tests.csproj -f net10.0
```
