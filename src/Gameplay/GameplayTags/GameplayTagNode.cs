using System.Collections.Generic;

namespace Gameplay;

/// <summary>GameplayTag 层级树节点，仅供 GameplayTagManager 内部使用。</summary>
internal sealed class GameplayTagNode
{
    internal readonly string Name;     // 短名（如 "Fire"）
    internal readonly string FullName; // 全名（如 "Damage.Fire"）
    internal readonly int    Id;

    internal GameplayTagNode?         Parent;
    internal List<GameplayTagNode>    Children;

    internal GameplayTagNode(string name, string fullName, int id)
    {
        Name     = name;
        FullName = fullName;
        Id       = id;
        Children = new List<GameplayTagNode>();
    }
}
