using Friflo.Engine.ECS.Systems;

namespace Gameplay.Replication;

/// <summary>服务端每帧复制 System（挂 PostSimulation，Simulation 改完组件后跑 shadow-diff 发送）。</summary>
public sealed class ReplicationServerSystem : BaseSystem
{
    private readonly ReplicationServer server;

    public ReplicationServerSystem(ReplicationServer server) => this.server = server;

    protected override void OnUpdateGroup() => server.Tick();
}
