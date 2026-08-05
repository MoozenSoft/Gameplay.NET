// tests/Gameplay.Tests/Gameplay.Tests.Tasks/AbilityActivateListenerSystemTests.cs
namespace Gameplay.Tests.Tasks;

using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Gameplay.Abilities;
using Gameplay.Tasks;
using Gameplay.Tags;
using Xunit;

/// <summary>对应 src/Gameplay/Gameplay.Tasks/Systems/AbilityActivateListenerSystem.cs——事件驱动：Ability 激活 → Task 完成。</summary>
public class AbilityActivateListenerSystemTests
{
    private static readonly GameplayTag FireTag = CreateTag("Test.Fire");
    private static readonly GameplayTag IceTag = CreateTag("Test.Ice");

    private static GameplayTag CreateTag(string name)
    {
        GameplayTagManager.RegisterTags(name);
        return GameplayTag.Request(name);
    }

    private static (Entity Owner, Entity Task, AbilityActivationManager ActivationManager, SystemRoot Root) Setup(
        GameplayTagContainer? abilityTags)
    {
        var store = new EntityStore();
        var mgr = new AttributeAggregatorManager();
        var activationManager = new AbilityActivationManager(new EffectSystem(mgr));
        var owner = store.CreateEntity();

        var task = TaskBuilder.WaitAbilityActivate(store, abilityTags, character: owner, owner: store.CreateEntity());

        var root = new SystemRoot(store) { new AbilityActivateListenerSystem(activationManager, store) };
        return (owner, task, activationManager, root);
    }

    private static void GrantAbility(Entity owner, GameplayAbility ability)
    {
        owner.AddComponent(new AbilityCollectionComponent
        {
            Specs = new[] { new AbilitySpec { Ability = ability, Handle = default } },
        });
    }

    private static ETaskState GetState(Entity task)
        => task.GetComponent<TaskStateComponent>().State;

    [Fact]
    public void MatchingAbility_CompletesTask()
    {
        var (owner, task, activationManager, root) = Setup(new GameplayTagContainer { FireTag });

        GrantAbility(owner, new GameplayAbility { AssetTags = new GameplayTagContainer { FireTag } });

        root.Update(new UpdateTick(0.16f, 0)); // Pending → Running（注册激活事件）
        Assert.Equal(ETaskState.Running, GetState(task));

        activationManager.TryActivateAbility(new AbilityActivationRequest { Owner = owner });

        Assert.Equal(ETaskState.Done, GetState(task));
    }

    [Fact]
    public void NonMatchingAbility_DoesNotComplete()
    {
        var (owner, task, activationManager, root) = Setup(new GameplayTagContainer { FireTag });

        GrantAbility(owner, new GameplayAbility { AssetTags = new GameplayTagContainer { IceTag } });

        root.Update(new UpdateTick(0.16f, 0)); // Pending → Running

        activationManager.TryActivateAbility(new AbilityActivationRequest { Owner = owner });

        Assert.Equal(ETaskState.Running, GetState(task));
    }

    [Fact]
    public void EmptyTags_MatchesAnyAbility()
    {
        var (owner, task, activationManager, root) = Setup(null);

        GrantAbility(owner, new GameplayAbility { AssetTags = new GameplayTagContainer { IceTag } });

        root.Update(new UpdateTick(0.16f, 0)); // Pending → Running

        activationManager.TryActivateAbility(new AbilityActivationRequest { Owner = owner });

        Assert.Equal(ETaskState.Done, GetState(task));
    }

    [Fact]
    public void FailedActivation_DoesNotComplete()
    {
        var (owner, task, activationManager, root) = Setup(null);

        GrantAbility(owner, new GameplayAbility
        {
            AssetTags = new GameplayTagContainer { FireTag },
            // Requirements 不通过 → 激活失败 → 不触发事件
            Requirements = new IAbilityRequirement[]
            {
                new RejectRequirement(),
            },
        });

        root.Update(new UpdateTick(0.16f, 0)); // Pending → Running

        activationManager.TryActivateAbility(new AbilityActivationRequest { Owner = owner });

        Assert.Equal(ETaskState.Running, GetState(task));
    }

    [Fact]
    public void OtherCharacterActivation_DoesNotComplete()
    {
        var (owner, task, activationManager, root) = Setup(new GameplayTagContainer { FireTag });

        GrantAbility(owner, new GameplayAbility { AssetTags = new GameplayTagContainer { FireTag } });

        root.Update(new UpdateTick(0.16f, 0)); // Pending → Running（注册激活事件）

        // 其他角色激活匹配的 Ability——监听者是 owner，不应完成
        var other = owner.Store.CreateEntity();
        GrantAbility(other, new GameplayAbility { AssetTags = new GameplayTagContainer { FireTag } });
        activationManager.TryActivateAbility(new AbilityActivationRequest { Owner = other });

        Assert.Equal(ETaskState.Running, GetState(task));
    }

    /// <summary>测试用：永远拒绝的 Requirement。</summary>
    private sealed class RejectRequirement : IAbilityRequirement
    {
        public bool Evaluate(Entity owner, AbilitySpec spec, in AbilityActivationRequest request) => false;
    }
}
