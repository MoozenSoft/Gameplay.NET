# 项目架构（samples 示例项目）

> **本文件描述 `samples/` 下 5 个示例项目**（Gameplay.Infrastructure / Gameplay.RPG / Gameplay.Client / Gameplay.Server / Gameplay.Host）的架构，参照 `.claude/gameplay-architecture.md` 的结构编写。
> 各节目前为骨架（占位符标注【替换】），随 samples 项目实现逐步填充实际内容。

## 章节结构来源

| 章节 | 建议 | 示例来源 |
|------|------|----------|
| ECS | 说明使用的 ECS 框架与「什么进 ECS、什么用 POCO」的分界线 | gameplay-architecture.md「ECS」节 |
| 功能蓝图 | 用表格列出功能与实现方式（Component/System/POCO），标注优先级 | gameplay-architecture.md「功能蓝图」节 |
| 核心子系统 | 每个子系统一个小节：概念映射表 + 核心设计原则 | gameplay-architecture.md「GAS」节 |
| 状态同步 | 网络模型、同步粒度、权威/预测职责划分 | gameplay-architecture.md「状态同步」节 |
| 编译配置 | 编译宏表格 + 宏组合表 + 条件编译示例 | gameplay-architecture.md「编译时宏」节 |
| 运行时模式 | 模式枚举 + 编译时宏与运行时判断的分工 | gameplay-architecture.md「运行时模式判断」节 |
| 多目标 | TFM 表格 + TFM 条件编译示例 | gameplay-architecture.md「多目标」节 |

---

## ECS

> 【替换】说明 ECS 框架选型（如 Friflo.Engine.ECS），以及「不是所有功能都进 ECS」的分界线——
> 多 Entity 批量遍历 → Component + System；单例服务 / 消息路由 / 基础设施 → 普通对象（POCO）。

**新 Feature 评估**：加入前自问三题——

| 问题 | ECS ✅ | POCO ✅ |
|------|--------|---------|
| 遍历大量 Entity？ | 是 | 否（≤1 个 Entity） |
| 核心是数据还是规则？ | 数据驱动 | 规则/流程驱动 |
| 每帧 Tick 还是事件触发？ | 每帧 Tick | 事件触发 |

## 功能蓝图

> 【替换】列出项目功能清单，标注优先级与实现方式。

### ECS 域（Component + Entity + System）

| 优先级 | 功能 | 实现 |
|--------|------|------|
| 【替换】必须 | 【替换】功能名 | 【替换】Component/System 实现说明 |

### 非 ECS 域（POCO / 独立服务）

| 优先级 | 功能 | 实现 |
|--------|------|------|
| 【替换】必须 | 【替换】功能名 | 【替换】独立服务实现说明 |

## 核心子系统

> 【替换】每个子系统一个小节。参考结构：概念映射表（概念 → ECS 实现）+ 核心设计原则。

### 子系统一

| 概念 | ECS 实现 | 说明 |
|------|----------|------|
| 【替换】概念 | 【替换】Component/System | 【替换】说明 |

## 状态同步

> 【替换】说明网络模型（如服务端权威 + 客户端预测/回滚）、同步管理单位、职责划分表。

| 角色 | 职责 |
|------|------|
| 【替换】Server | 【替换】权威逻辑与广播 |
| 【替换】Client | 【替换】预测与回滚 |

## 编译配置

> 【替换】编译宏表格。通过 MSBuild `DefineConstants` 传入，控制编译模式。

| 宏 | 用途 |
|----|------|
| 【替换】`XXX` | 【替换】用途 |

```csharp
#if XXX
    // 【替换】仅该模式编译的代码
#endif
```

## 运行时模式

> 【替换】运行时模式枚举。编译时宏决定代码是否编译进程序集，运行时枚举决定逻辑分支，两层判断配合使用。

```csharp
public enum NetMode
{
    Standalone,      // 单机（无网络）
    Client,          // 客户端
    DedicatedServer, // 专用服务器
    ListenServer     // 监听服务器 (Host)
}
```

## 多目标

> 【替换】目标平台 TFM 表格与条件编译示例。

| TFM | 定位 | 典型使用场景 |
|-----|------|-------------|
| 【替换】netstandard2.1 | 【替换】最大兼容 | 【替换】引擎集成 |
| 【替换】net10.0 | 【替换】最新 API | 【替换】独立进程 |

```csharp
#if NET
    // 【替换】最新 TFM 专有 API
#endif

#if NETSTANDARD2_1
    // 【替换】旧 TFM 回退实现
#endif
```
