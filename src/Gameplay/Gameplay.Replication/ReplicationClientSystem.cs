using System;
using Friflo.Engine.ECS.Systems;

namespace Gameplay.Replication;

/// <summary>客户端每帧镜像 System（挂 PreSimulation，模拟前应用服务端状态）。</summary>
public sealed class ReplicationClientSystem : BaseSystem
{
    private readonly ReplicationClient client;
    private readonly IReplicationClientTransport transport;

    public ReplicationClientSystem(ReplicationClient client, IReplicationClientTransport transport)
    {
        this.client = client;
        this.transport = transport;
    }

    protected override void OnUpdateGroup()
    {
        while (transport.TryReceiveFromServer(out var payload))
            client.ApplyServerPacket(payload);
    }
}
