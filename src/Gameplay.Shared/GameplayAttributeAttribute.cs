using System;

namespace Gameplay.Abilities;

/// <summary>标记 AttributeSet struct 中的字段为 GameplayAttribute。SG 扫描生成访问器。</summary>
[AttributeUsage(AttributeTargets.Field)]
public class GameplayAttributeAttribute : System.Attribute { }
