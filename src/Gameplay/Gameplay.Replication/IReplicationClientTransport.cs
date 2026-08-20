using System;

namespace Gameplay.Replication;

/// <summary>客户端传输。</summary>
public interface IReplicationClientTransport
{
    bool TryReceiveFromServer(out ReadOnlySpan<byte> payload);
    void SendToServer(ReadOnlySpan<byte> payload); // v1 预留
}
