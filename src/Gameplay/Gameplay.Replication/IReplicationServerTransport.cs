using System;
using System.Collections.Generic;

namespace Gameplay.Replication;

/// <summary>服务端传输（纯消息管道，不持有客户端集合）。</summary>
public interface IReplicationServerTransport
{
    void SendToClient(int clientId, ReadOnlySpan<byte> payload);
    bool TryReceiveFromClient(int clientId, out ReadOnlySpan<byte> payload); // v1 预留
}
