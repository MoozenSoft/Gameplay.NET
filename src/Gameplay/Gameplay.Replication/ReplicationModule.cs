using Gameplay.Core;

namespace Gameplay.Replication;

/// <summary>状态复制模块——按 NetMode 挂载服务端/客户端复制。</summary>
/// <remarks>
/// 使用方必须在挂载本模块之外、首次 <see cref="World.Update"/> 之前调用一次
/// <c>ReplicatedComponentRegistration.RegisterAll()</c>（源生成器生成，启动阶段调用一次即可），
/// 否则复制集为空、静默无复制。
/// </remarks>
public sealed class ReplicationModule : IModule
{
    public ReplicationServer? Server { get; }
    public ReplicationClient? Client { get; }

    public ReplicationModule(World world, IReplicationServerTransport? serverTransport, IReplicationClientTransport? clientTransport)
    {
        var netMode = world.NetMode;

#if GP_WITH_SERVER_CODE
        if ((netMode == ENetMode.DedicatedServer || netMode == ENetMode.ListenServer) && serverTransport != null)
        {
            var server = new ReplicationServer(world.Store, serverTransport);
            Server = server;
            world.RegisterService(server);
            world.AddSystem(new ReplicationSystem(server), ESimulationStage.PostSimulation);
            EntityLifecycle.Subscribe(world, server.HandleLifecycle);
        }
#endif

#if !GP_SERVER
        if (netMode == ENetMode.Client && clientTransport != null)
        {
            var client = new ReplicationClient(world.Store, clientTransport);
            Client = client;
            world.RegisterService(client);
            world.AddSystem(new ReplicationClientSystem(client, clientTransport), ESimulationStage.PreSimulation);
        }
#endif
    }
}
