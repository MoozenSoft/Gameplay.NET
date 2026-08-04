// tests/Gameplay.Tests/Gameplay.Tests.Tasks/TaskOwnerComponentTests.cs
namespace Gameplay.Tests.Tasks;

using Friflo.Engine.ECS;
using Gameplay.Tasks;
using Xunit;

public class TaskOwnerComponentTests
{
    [Fact]
    public void Default_Values()
    {
        var comp = new TaskOwnerComponent();
        Assert.Equal(default, comp.Owner);
        Assert.Equal(0, comp.TaskHandle);
    }
}
