# XML 文档注释规范

本项目的 `///` 注释规范——保证文档编译正确（无警告）且人类可读，贴近 Microsoft .NET 官方文档风格。参考第三方评估修订（2026-08）。

## 总原则

- **描述用中文，专业术语保留英文**（Entity、GameplayAttribute、Task、Done）——与 CLAUDE.md「注释使用中文」约定一致
- **描述语义（是什么），不描述实现（怎么做）**：

```csharp
// ✅ 语义
/// <summary>创建延时 Task。</summary>

// ❌ 实现
/// <summary>new 一个 Entity，然后加入 DelayTaskComponent。</summary>
```

- **不重复类型**——参数/字段类型 IDE 已显示，注释只说语义和约束
- **内部成员（private/internal/file）按需编写**——只有公开 API 强制；内部成员有非显然行为才写

## 一、`<summary>` 与 `<remarks>` 分工

**Summary 只描述"是什么"；为什么、限制、副作用、线程安全放 Remarks**——否则 Summary 越来越长：

```csharp
/// <summary>创建延时 Task。</summary>
/// <remarks>
/// <para>等待指定时间后完成（Done）。</para>
/// <para>Duration 为 0 时立即完成。</para>
/// </remarks>
```

## 二、防坑理由：写"约束"，不写"历史"

写**调用约束、生命周期、快照时机、是否允许修改**等客观事实；不写"因为 XXX 曾经出 Bug""否则以后别人会改坏"这类叙事：

```csharp
// ✅ 约束（客观）
/// <remarks>该快照仅用于插值计算。修改该值不会影响实际位置。</remarks>

// ❌ 历史/威胁叙事
/// <remarks>因为之前出过 bug，不要修改这个字段。</remarks>
```

## 三、组件注释：注明生命周期，不注明"谁写入"

```csharp
// ✅ 生命周期（稳定——未来多个 System 写入也不失效）
/// <summary>插值起点。</summary>
/// <remarks>初始化时设置一次，不应在运行过程中修改。</remarks>

// ❌ 作者（脆弱——MoveToSystem 改名或加 PredictionSystem 就失效）
/// <summary>由 MoveToSystem 写入的开始位置。</summary>
```

## 四、技术债/历史背景不写进 XML

循环依赖、TODO、架构决策属于 ADR / GitHub Issue / 代码 TODO——**XML 文档只描述 API**。否则技术债修复后所有 XML 都要改。

```csharp
// ❌ 技术债叙事（移出 XML）
/// <remarks>注：形成命名空间循环（技术债）。</remarks>
```

## 五、分段用 `<para>`，列表用 `<list>`，不用连续 `<br/>`

```csharp
/// <remarks>
/// <para>第一段。</para>
/// <para>第二段。</para>
/// </remarks>

/// <summary>支持：</summary>
/// <list type="bullet">
/// <item><description>Ability 使用。</description></item>
/// <item><description>AI 使用。</description></item>
/// </list>
```

## 六、符号：Unicode 优先，中文次之，转义兜底

```csharp
// ✅ 优先：Unicode 符号（≤ ≥ ≠ 等，XML 中无需转义）
/// <summary>CurrentValue ≤ Threshold。</summary>
/// <param name="duration">持续时间（≤ 0 表示立即完成）。</param>
/// <remarks>Duration ≠ 0 时才结算。</remarks>

// ✅ fallback 1：中文自然表达
/// <summary>CurrentValue 大于 Threshold。</summary>
/// <param name="count">等待次数（大于 0）。</param>

// ✅ fallback 2：合法转义（仅当 Unicode 与中文均不合适）
/// <param name="duration">持续时间（&lt;= 0 表示立即完成）。</param>

// ❌ 禁止：裸 < 会触发 CS1570（badly formed XML comment）
/// <summary>CurrentValue < Threshold。</summary>
```

**规则**：优先使用 Unicode 数学符号（`≤` `≥` `≠` 等）——XML 注释中合法、无需转义且更清晰；无法用符号表达时回退到中文自然表达；两者都不合适时才用合法转义（`&lt;=`/`&gt;=`/`!=`）。`<` 和 `&` 永远必须转义（裸写会触发 CS1570 / 非法 XML）。

**范围**：Unicode 优先仅针对**数学关系符号**（`≤` `≥` `≠` 等）。代码语法符号（如泛型尖括号 `Query<T>`）不进此规则——按中文自然表达（fallback 1）或转义（fallback 2，需精确展示代码语法时）处理，不使用 `⟨⟩`/`＜＞` 等近似字符替代。

## 七、代码引用

- 类型/成员引用用 `<see cref="..."/>`（可点击跳转）——`cref` 必须引用**存在的**类型（否则 CS1574）
- 泛型 cref：`<see cref="EntityStoreExtensions.CreateEntity{T1,T2,T3}"/>`（花括号原样写）
- 跨命名空间写全名：`<see cref="Gameplay.Interfaces.IInputService"/>`
- 正文引用参数用 `<paramref name="..."/>`（可点击）
- 关联 API 用 `<seealso cref="..."/>`

## 八、公开成员注释要求

- 公开成员（类型/方法/属性/字段/枚举）**必有 `<summary>`**（开启 GenerateDocumentationFile 后缺注释产生 CS1591）
- 方法加 `<param>`/`<returns>`（要么全写要么全不写——漏写产生 CS1573）
- 抛异常的方法加 `<exception>`：

```csharp
/// <exception cref="ArgumentException">Interval 必须大于 0。</exception>
public static Entity Repeat(...)
```

- 属性可加 `<value>`（描述属性语义）

## 九、`<inheritdoc/>`：override/实现避免复制注释

```csharp
/// <inheritdoc/>
public override void OnUpdate() { ... }
```

基类/接口已有注释时，override 用 `<inheritdoc/>` 或省略（不复制粘贴）。

## 十、`<param>` 专项

- `name` 与签名**精确匹配**（大小写敏感，否则 CS1573）
- 内容：**语义 + 约束**（取值范围、null 语义、默认行为、副作用），不说类型
- 特殊参数：可选参数说明默认值；out 说明写回内容；委托说明调用时机
- 泛型参数用 `<typeparam>`（不是 `<param>`）
- 有返回值的方法 `<returns>` 说明返回语义（含失败/无效场景）

## 十一、枚举成员逐个注释

```csharp
/// <summary>属性监听条件。</summary>
public enum EAttributeCondition
{
    /// <summary>值发生变化（相对注册时快照，Count 次）。</summary>
    Changed,
    /// <summary>CurrentValue 大于 Threshold。</summary>
    Above,
}
```

## 现状与建议

- 技术债注释（如 CommitPhaseListener 的循环依赖说明）：保留为普通 `//` 注释（非 XML）
