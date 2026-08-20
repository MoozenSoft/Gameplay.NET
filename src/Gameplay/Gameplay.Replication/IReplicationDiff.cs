using Friflo.Engine.ECS;

namespace Gameplay.Replication;

/// <summary>组件相等判定（shadow-diff 用）——字段级比较。</summary>
public interface IReplicationDiff<T> where T : struct, IComponent
{
    bool Equals(in T a, in T b);
}
