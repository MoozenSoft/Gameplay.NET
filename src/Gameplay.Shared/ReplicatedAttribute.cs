using System;

namespace Gameplay;

/// <summary>标记 struct 组件参与网络复制。SG 扫描生成 serializer + diff + RegisterAll。</summary>
[AttributeUsage(AttributeTargets.Struct)]
public class ReplicatedAttribute : System.Attribute { }
