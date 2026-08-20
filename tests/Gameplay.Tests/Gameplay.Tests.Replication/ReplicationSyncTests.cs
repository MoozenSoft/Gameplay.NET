using Friflo.Engine.ECS;
using Gameplay.Core;
using Gameplay.Replication;
using Xunit;

namespace Gameplay.Tests.Replication;

public class ReplicationSyncTests
{
    [Fact]
    public void ServerChange_MirrorsToClient()
    {
        SerializerRegistry.Register(new SyncTestSerializer());
        ReplicationRegistry.Register<SyncTestComponent>(new SyncTestDiff());

        // 服务端权威 World
        var serverWorld = new World(ENetMode.DedicatedServer);
        var serverTransport = new LoopbackServerTransport();

        // 客户端镜像 World
        var clientWorld = new World(ENetMode.Client);
        var clientTransport = new LoopbackClientTransport();
        serverTransport.RegisterClient(0, clientTransport.Queue);

        var serverModule = new ReplicationModule(serverWorld, serverTransport, null);
        var clientModule = new ReplicationModule(clientWorld, null, clientTransport);
        serverModule.Server!.AddClient(0);

        // 服务端创建实体
        var serverEntity = serverWorld.Store.CreateEntity();
        serverEntity.AddComponent(new SyncTestComponent { Value = 10 });

        // 跑一帧：服务端发送 → 客户端接收应用
        serverWorld.Update(0.16f);
        clientWorld.Update(0.16f);

        var mirror = clientModule.Client!.GetMirror(serverModule.Server!.GetNetworkId(serverEntity.Id));
        Assert.False(mirror.IsNull);
        Assert.Equal(10, mirror.GetComponent<SyncTestComponent>().Value);
    }

    [Fact]
    public void Dirty_OnlySendsChangedComponent()
    {
        SerializerRegistry.Register(new SyncTestSerializer());
        ReplicationRegistry.Register<SyncTestComponent>(new SyncTestDiff());

        var serverWorld = new World(ENetMode.DedicatedServer);
        var serverTransport = new LoopbackServerTransport();

        var clientWorld = new World(ENetMode.Client);
        var clientTransport = new LoopbackClientTransport();
        serverTransport.RegisterClient(0, clientTransport.Queue);

        var serverModule = new ReplicationModule(serverWorld, serverTransport, null);
        var clientModule = new ReplicationModule(clientWorld, null, clientTransport);
        serverModule.Server!.AddClient(0);

        var serverEntity = serverWorld.Store.CreateEntity();
        serverEntity.AddComponent(new SyncTestComponent { Value = 10 });

        // 第一帧：服务端发 Spawn → 客户端建镜像
        serverWorld.Update(0.16f);
        clientWorld.Update(0.16f);

        var netId = serverModule.Server!.GetNetworkId(serverEntity.Id);
        var firstMirror = clientModule.Client!.GetMirror(netId);
        Assert.False(firstMirror.IsNull);
        Assert.Equal(10, firstMirror.GetComponent<SyncTestComponent>().Value);

        // 服务端改组件值（ref write）——只改了组件，未增删实体
        ref var comp = ref serverEntity.GetComponent<SyncTestComponent>();
        comp.Value = 20;

        // 第二帧：服务端应只发 EUpdate（dirty 只发变化组件），不再重复 Spawn
        serverWorld.Update(0.16f);
        Assert.Single(clientTransport.Queue);                                   // 只有一条包
        Assert.Equal((byte)EReplicationPacketType.Update, clientTransport.Queue.Peek()[0]);   // 且是 Update 而非 Spawn
        clientWorld.Update(0.16f);

        // 镜像未被重建（收到 EUpdate 而非第二个 Spawn → GetMirror 仍指向同一实体），且值已更新
        var secondMirror = clientModule.Client!.GetMirror(netId);
        Assert.False(secondMirror.IsNull);
        Assert.Equal(firstMirror.Id, secondMirror.Id);
        Assert.Equal(20, secondMirror.GetComponent<SyncTestComponent>().Value);
    }

    [Fact]
    public void OwnerBased_Visibility_FiltersClients()
    {
        SerializerRegistry.Register(new SyncTestSerializer());
        ReplicationRegistry.Register<SyncTestComponent>(new SyncTestDiff());

        var serverWorld = new World(ENetMode.DedicatedServer);
        var serverTransport = new LoopbackServerTransport();

        // 两个客户端，各自独立传输/世界
        var clientWorld0 = new World(ENetMode.Client);
        var clientTransport0 = new LoopbackClientTransport();
        serverTransport.RegisterClient(0, clientTransport0.Queue);

        var clientWorld1 = new World(ENetMode.Client);
        var clientTransport1 = new LoopbackClientTransport();
        serverTransport.RegisterClient(1, clientTransport1.Queue);

        var serverModule = new ReplicationModule(serverWorld, serverTransport, null);
        var clientModule0 = new ReplicationModule(clientWorld0, null, clientTransport0);
        var clientModule1 = new ReplicationModule(clientWorld1, null, clientTransport1);
        serverModule.Server!.AddClient(0);
        serverModule.Server!.AddClient(1);

        // 先加 Owner 再加复制组件：AddToBubbles 时能读到归属，只进客户端 0 的 Bubble
        var serverEntity = serverWorld.Store.CreateEntity();
        serverEntity.AddComponent(new OwnerComponent { PlayerId = 0 });
        serverEntity.AddComponent(new SyncTestComponent { Value = 5 });

        serverWorld.Update(0.16f);
        clientWorld0.Update(0.16f);
        clientWorld1.Update(0.16f);

        var netId = serverModule.Server!.GetNetworkId(serverEntity.Id);
        Assert.False(clientModule0.Client!.GetMirror(netId).IsNull);   // 归属客户端 0 → 收到
        Assert.True(clientModule1.Client!.GetMirror(netId).IsNull);    // 非归属客户端 1 → 不收到
    }

    [Fact]
    public void LateJoin_ReceivesFullSnapshot()
    {
        SerializerRegistry.Register(new SyncTestSerializer());
        ReplicationRegistry.Register<SyncTestComponent>(new SyncTestDiff());

        var serverWorld = new World(ENetMode.DedicatedServer);
        var serverTransport = new LoopbackServerTransport();

        var clientWorld = new World(ENetMode.Client);
        var clientTransport = new LoopbackClientTransport();
        serverTransport.RegisterClient(0, clientTransport.Queue);

        var serverModule = new ReplicationModule(serverWorld, serverTransport, null);
        var clientModule = new ReplicationModule(clientWorld, null, clientTransport);

        // 晚加入：先创建实体，再 AddClient（AddClient 时实体已存在 → 需回填 Bubble 并全量快照）
        var serverEntity = serverWorld.Store.CreateEntity();
        serverEntity.AddComponent(new SyncTestComponent { Value = 30 });

        serverModule.Server!.AddClient(0);

        serverWorld.Update(0.16f);
        clientWorld.Update(0.16f);

        var mirror = clientModule.Client!.GetMirror(serverModule.Server!.GetNetworkId(serverEntity.Id));
        Assert.False(mirror.IsNull);
        Assert.Equal(30, mirror.GetComponent<SyncTestComponent>().Value);
    }

    [Fact]
    public void Reconnect_PrunesStaleMirrorViaFullSnapshot()
    {
        SerializerRegistry.Register(new SyncTestSerializer());
        ReplicationRegistry.Register<SyncTestComponent>(new SyncTestDiff());

        var serverWorld = new World(ENetMode.DedicatedServer);
        var serverTransport = new LoopbackServerTransport();

        var clientWorld = new World(ENetMode.Client);
        var clientTransport = new LoopbackClientTransport();
        serverTransport.RegisterClient(0, clientTransport.Queue);

        var serverModule = new ReplicationModule(serverWorld, serverTransport, null);
        var clientModule = new ReplicationModule(clientWorld, null, clientTransport);
        serverModule.Server!.AddClient(0);

        // 服务端创建实体 → 客户端首帧全量快照收到
        var serverEntity = serverWorld.Store.CreateEntity();
        serverEntity.AddComponent(new SyncTestComponent { Value = 5 });
        serverWorld.Update(0.16f);
        clientWorld.Update(0.16f);

        var netId = serverModule.Server!.GetNetworkId(serverEntity.Id);
        Assert.False(clientModule.Client!.GetMirror(netId).IsNull);

        // 断开连接 → 服务端删除实体（已移除的客户端收不到 despawn，留下陈旧镜像）
        serverModule.Server!.RemoveClient(0);
        serverEntity.DeleteEntity();
        while (clientTransport.Queue.Count > 0) clientTransport.Queue.Dequeue();

        // 重连 → 空全量快照 → 客户端 reconcile 删多余清除陈旧镜像
        serverModule.Server!.AddClient(0);
        serverWorld.Update(0.16f);
        clientWorld.Update(0.16f);

        Assert.True(clientModule.Client!.GetMirror(netId).IsNull);
    }
}
