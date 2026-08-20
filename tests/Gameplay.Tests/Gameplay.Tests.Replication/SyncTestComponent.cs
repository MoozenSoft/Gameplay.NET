using Friflo.Engine.ECS;
using Gameplay.Core;
using Gameplay.Replication;

namespace Gameplay.Tests.Replication;

/// <summary>复制逻辑测试用的组件（手写 serializer/diff，Task 9 前替代 SG 生成）。</summary>
public struct SyncTestComponent : IComponent
{
    public int Value;
}

/// <summary>SyncTestComponent 手写序列化器。</summary>
public sealed class SyncTestSerializer : IComponentSerializer<SyncTestComponent>
{
    public void Write(in SyncTestComponent c, ref ByteWriter w) => w.Write(c.Value);
    public void Read(ref SyncTestComponent c, ref ByteReader r) => c.Value = r.ReadInt();
}

/// <summary>SyncTestComponent 手写 diff。</summary>
public readonly struct SyncTestDiff : IReplicationDiff<SyncTestComponent>
{
    public bool Equals(in SyncTestComponent a, in SyncTestComponent b) => a.Value == b.Value;
}

/// <summary>仅用于验证「未注册序列化器 → fail-fast」的组件（全测试中不注册 serializer/diff）。</summary>
public struct SyncTestNoSerializerComponent : IComponent
{
    public int Value;
}

/// <summary>SyncTestNoSerializerComponent 手写 diff（仅用于 Register 调用，实际因缺 serializer 不会走到）。</summary>
public readonly struct SyncTestNoSerializerDiff : IReplicationDiff<SyncTestNoSerializerComponent>
{
    public bool Equals(in SyncTestNoSerializerComponent a, in SyncTestNoSerializerComponent b) => a.Value == b.Value;
}
