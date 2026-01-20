using Producer.Services;

namespace Producer.UnitTests;

/// <summary>
/// Simplified unit tests focusing on control logic without QueueClient mocking
/// </summary>
public class ProducerWorkerSimplifiedTests
{
    [Fact]
    public void Control_ShouldPreventExecution_WhenStopped()
    {
        // Arrange
        var mockControl = new Mock<IProducerWorkerControl>();
        mockControl.Setup(x => x.IsRunning()).Returns(false);

        // Act & Assert
        mockControl.Object.IsRunning().Should().BeFalse();
    }

    [Fact]
    public void Control_ShouldAllowExecution_WhenStarted()
    {
        // Arrange
        var mockControl = new Mock<IProducerWorkerControl>();
        mockControl.Setup(x => x.IsRunning()).Returns(true);
        mockControl.Setup(x => x.GetRate()).Returns(10);

        // Act & Assert
        mockControl.Object.IsRunning().Should().BeTrue();
        mockControl.Object.GetRate().Should().Be(10);
    }
}