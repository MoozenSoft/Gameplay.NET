using System;
using System.Collections.Generic;
using Gameplay.Replication;

namespace Gameplay.Tests.Replication;

/// <summary>内存回环服务端传输——把消息路由到各客户端的入队。</summary>
public sealed class LoopbackServerTransport : IReplicationServerTransport
{
    private readonly Dictionary<int, Queue<byte[]>> clientQueues = new();
    private readonly List<int> clientIds = new();

    public void RegisterClient(int clientId, Queue<byte[]> queue)
    {
        clientQueues[clientId] = queue;
        clientIds.Add(clientId);
    }

    public IReadOnlyList<int> ClientIds => clientIds;

    public void SendToClient(int clientId, ReadOnlySpan<byte> payload)
        => clientQueues[clientId].Enqueue(payload.ToArray());

    public bool TryReceiveFromClient(int clientId, out ReadOnlySpan<byte> payload)
    {
        payload = default;
        return false;   // v1 无上行
    }
}

/// <summary>内存回环客户端传输——从本客户端的入队拉取。</summary>
public sealed class LoopbackClientTransport : IReplicationClientTransport
{
    private readonly Queue<byte[]> incoming = new();

    public Queue<byte[]> Queue => incoming;

    public bool TryReceiveFromServer(out ReadOnlySpan<byte> payload)
    {
        if (incoming.Count == 0)
        {
            payload = default;
            return false;
        }
        var data = incoming.Dequeue();
        payload = data;
        return true;
    }

    public void SendToServer(ReadOnlySpan<byte> payload) { }   // v1 无上行
}
