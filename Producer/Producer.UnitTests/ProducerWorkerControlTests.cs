using Microsoft.Extensions.Options;
using Producer.Configuration;
using Producer.Services;

namespace Producer.UnitTests;

/// <summary>
/// Unit tests for ProducerWorkerControl - validates thread-safe state management.
/// </summary>
public class ProducerWorkerControlTests
{
    [Fact]
    public void Constructor_ShouldInitializeWithDefaultRate_WhenOptionsAreNull()
    {
        // Arrange & Act
        var control = new ProducerWorkerControl(null!);

        // Assert
        control.GetRate().Should().Be(1);
        control.IsRunning().Should().BeFalse();
    }

    [Fact]
    public void Constructor_ShouldInitializeWithConfiguredRate()
    {
        // Arrange
        var options = CreateOptionsMonitor(messagesPerSecond: 10);

        // Act
        var control = new ProducerWorkerControl(options);

        // Assert
        control.GetRate().Should().Be(10);
    }

    [Fact]
    public void Constructor_ShouldEnforceMinimumRateOfOne_WhenConfiguredRateIsZero()
    {
        // Arrange
        var options = CreateOptionsMonitor(messagesPerSecond: 0);

        // Act
        var control = new ProducerWorkerControl(options);

        // Assert
        control.GetRate().Should().Be(1);
    }

    [Fact]
    public void Constructor_ShouldEnforceMinimumRateOfOne_WhenConfiguredRateIsNegative()
    {
        // Arrange
        var options = CreateOptionsMonitor(messagesPerSecond: -5);

        // Act
        var control = new ProducerWorkerControl(options);

        // Assert
        control.GetRate().Should().Be(1);
    }

    [Fact]
    public void StartProducing_ShouldSetRunningStateToTrue()
    {
        // Arrange
        var control = new ProducerWorkerControl(null!);

        // Act
        control.StartProducing();

        // Assert
        control.IsRunning().Should().BeTrue();
    }

    [Fact]
    public void StopProducing_ShouldSetRunningStateToFalse()
    {
        // Arrange
        var control = new ProducerWorkerControl(null!);
        control.StartProducing();

        // Act
        control.StopProducing();

        // Assert
        control.IsRunning().Should().BeFalse();
    }

    [Fact]
    public void SetRate_ShouldUpdateRate()
    {
        // Arrange
        var control = new ProducerWorkerControl(null!);

        // Act
        control.SetRate(25);

        // Assert
        control.GetRate().Should().Be(25);
    }

    [Fact]
    public void SetRate_ShouldEnforceMinimumOfOne_WhenSettingZero()
    {
        // Arrange
        var control = new ProducerWorkerControl(null!);

        // Act
        control.SetRate(0);

        // Assert
        control.GetRate().Should().Be(1);
    }

    [Fact]
    public void SetRate_ShouldEnforceMinimumOfOne_WhenSettingNegative()
    {
        // Arrange
        var control = new ProducerWorkerControl(null!);

        // Act
        control.SetRate(-10);

        // Assert
        control.GetRate().Should().Be(1);
    }

    [Fact]
    public void OnChange_ShouldUpdateRateWhenConfigurationChanges()
    {
        // Arrange
        var mockMonitor = new Mock<IOptionsMonitor<QueueOptions>>();
        var initialOptions = new QueueOptions { MessagesPerSecond = 5 };
        
        mockMonitor.Setup(x => x.CurrentValue).Returns(initialOptions);
        
        IDisposable? changeToken = null;
        mockMonitor.Setup(x => x.OnChange(It.IsAny<Action<QueueOptions, string?>>()))
            .Callback<Action<QueueOptions, string?>>((listener) =>
            {
                changeToken = Mock.Of<IDisposable>();
                // Simulate configuration change
                var newOptions = new QueueOptions { MessagesPerSecond = 15 };
                listener(newOptions, null);
            })
            .Returns(() => changeToken!);

        // Act
        var control = new ProducerWorkerControl(mockMonitor.Object);

        // Assert
        control.GetRate().Should().Be(15);
    }

    [Fact]
    public async Task MultipleThreads_ShouldHandleConcurrentOperations()
    {
        // Arrange
        var control = new ProducerWorkerControl(null!);
        var tasks = new List<Task>();

        // Act - simulate concurrent access from multiple threads
        for (int i = 0; i < 10; i++)
        {
            int rate = i + 1;
            tasks.Add(Task.Run(() =>
            {
                control.StartProducing();
                control.SetRate(rate);
                var currentRate = control.GetRate();
                var isRunning = control.IsRunning();
                control.StopProducing();
            }));
        }

        await Task.WhenAll(tasks); // ✅ Async await

        // Assert - no exceptions should occur, and final state should be valid
        control.GetRate().Should().BeGreaterThanOrEqualTo(1);
    }

    // Helper method to create IOptionsMonitor mock
    private IOptionsMonitor<QueueOptions> CreateOptionsMonitor(int messagesPerSecond)
    {
        var mockMonitor = new Mock<IOptionsMonitor<QueueOptions>>();
        var options = new QueueOptions { MessagesPerSecond = messagesPerSecond };
        
        mockMonitor.Setup(x => x.CurrentValue).Returns(options);
        mockMonitor.Setup(x => x.OnChange(It.IsAny<Action<QueueOptions, string?>>()))
            .Returns(Mock.Of<IDisposable>());
        
        return mockMonitor.Object;
    }
}