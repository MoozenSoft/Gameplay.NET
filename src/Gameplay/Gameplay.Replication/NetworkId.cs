namespace Gameplay.Replication;

/// <summary>跨进程实体网络身份（服务端分配自增正数，0 = Invalid）。</summary>
public readonly struct NetworkId
{
    public readonly int Value;

    public NetworkId(int value) => Value = value;

    /// <summary>是否有效（正数）。</summary>
    public bool IsValid => Value > 0;

    public static NetworkId Invalid => default;
}
