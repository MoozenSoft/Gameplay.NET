namespace Gameplay;

/// <summary>
/// 网络运行模式。
/// </summary>
public enum NetMode
{
    /// <summary>单机模式（无网络）。</summary>
    Standalone,

    /// <summary>客户端模式。</summary>
    Client,

    /// <summary>专用服务器模式。</summary>
    DedicatedServer,

    /// <summary>监听服务器模式（Host）。</summary>
    ListenServer,
}
