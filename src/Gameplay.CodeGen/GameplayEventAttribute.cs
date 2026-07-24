using System;

namespace Gameplay.Abilities;

/// <summary>标记 struct 为 GameplayEvent。SG 扫描生成 EventId + Registry。</summary>
[AttributeUsage(AttributeTargets.Struct)]
public class GameplayEventAttribute : System.Attribute
{
    public string Tag { get; set; } = string.Empty;
}
