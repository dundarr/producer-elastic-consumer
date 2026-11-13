using Microsoft.Extensions.Logging;
using Producer.Services;
using Producer.Workers;
using System.Collections.Frozen;

namespace Producer.UnitTests;

/// <summary>
/// Unit tests for ConsumerCleanupWorker - validates consumer cleanup logic.
/// </summary>
public class ConsumerCleanupWorkerTests
{
    private readonly Mock<IConsumerRegistry> _mockRegistry;

    public ConsumerCleanupWorkerTests()
    {
        _mockRegistry = new Mock<IConsumerRegistry>();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldRemoveStaleConsumers()
    {
        // Arrange
        var activeConsumer = Guid.NewGuid();
        var staleConsumer = Guid.NewGuid();

        var consumers = new Dictionary<Guid, DateTime>
        {
            { activeConsumer, DateTime.UtcNow }, // Active
            { staleConsumer, DateTime.UtcNow.AddSeconds(-35) } // Stale (older than 30 seconds)
        }.ToFrozenDictionary();

        _mockRegistry
            .Setup(x => x.GetRegisteredAsync())
            .ReturnsAsync(consumers);

        var worker = new ConsumerCleanupWorker(_mockRegistry.Object);

        using var cts = new CancellationTokenSource();

        // Act
        var executeTask = worker.StartAsync(cts.Token);
        await Task.Delay(500); // Let it run one cleanup cycle
        cts.Cancel();

        try
        {
            await executeTask;
        }
        catch (OperationCanceledException)
        {
            // Expected
        }

        // Assert - stale consumer should be unregistered
        _mockRegistry.Verify(x => x.RemoveIfUnchanged(staleConsumer, It.IsAny<DateTime>()), Times.AtLeastOnce);

        // Active consumer should NOT be unregistered
        _mockRegistry.Verify(x => x.RemoveIfUnchanged(activeConsumer, It.IsAny<DateTime>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldNotRemoveActiveConsumers()
    {
        // Arrange
        var activeConsumer1 = Guid.NewGuid();
        var activeConsumer2 = Guid.NewGuid();

        var consumers = new Dictionary<Guid, DateTime>
        {
            { activeConsumer1, DateTime.UtcNow.AddSeconds(-5) },
            { activeConsumer2, DateTime.UtcNow.AddSeconds(-3) }
        }.ToFrozenDictionary();

        _mockRegistry
            .Setup(x => x.GetRegisteredAsync())
            .ReturnsAsync(consumers);

        var worker = new ConsumerCleanupWorker(_mockRegistry.Object);

        using var cts = new CancellationTokenSource();

        // Act
        var executeTask = worker.StartAsync(cts.Token);
        await Task.Delay(500);
        cts.Cancel();

        try
        {
            await executeTask;
        }
        catch (OperationCanceledException)
        {
            // Expected
        }

        // Assert - no consumers should be unregistered
        _mockRegistry.Verify(x => x.RemoveIfUnchanged(It.IsAny<Guid>(), It.IsAny<DateTime>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldHandleEmptyRegistry()
    {
        // Arrange
        var emptyRegistry = new Dictionary<Guid, DateTime>().ToFrozenDictionary();

        _mockRegistry
            .Setup(x => x.GetRegisteredAsync())
            .ReturnsAsync(emptyRegistry);

        var worker = new ConsumerCleanupWorker(_mockRegistry.Object);

        using var cts = new CancellationTokenSource();

        // Act
        var executeTask = worker.StartAsync(cts.Token);
        await Task.Delay(500);
        cts.Cancel();

        var act = async () =>
        {
            try
            {
                await executeTask;
            }
            catch (OperationCanceledException)
            {
                // Expected
            }
        };

        // Assert - should not throw
        await act.Should().NotThrowAsync();

        // No unregister calls should be made
        _mockRegistry.Verify(x => x.RemoveIfUnchanged(It.IsAny<Guid>(), It.IsAny<DateTime>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldCleanupMultipleStaleConsumers()
    {
        // Arrange
        var activeConsumer = Guid.NewGuid();
        var staleConsumer1 = Guid.NewGuid();
        var staleConsumer2 = Guid.NewGuid();
        var staleConsumer3 = Guid.NewGuid();

        var consumers = new Dictionary<Guid, DateTime>
        {
            { activeConsumer, DateTime.UtcNow },
            { staleConsumer1, DateTime.UtcNow.AddSeconds(-32) },
            { staleConsumer2, DateTime.UtcNow.AddSeconds(-40) },
            { staleConsumer3, DateTime.UtcNow.AddSeconds(-60) }
        }.ToFrozenDictionary();

        _mockRegistry
            .Setup(x => x.GetRegisteredAsync())
            .ReturnsAsync(consumers);

        var worker = new ConsumerCleanupWorker(_mockRegistry.Object);

        using var cts = new CancellationTokenSource();

        // Act
        var executeTask = worker.StartAsync(cts.Token);
        await Task.Delay(500);
        cts.Cancel();

        try
        {
            await executeTask;
        }
        catch (OperationCanceledException)
        {
            // Expected
        }

        // Assert - all stale consumers should be unregistered
        _mockRegistry.Verify(x => x.RemoveIfUnchanged(staleConsumer1, It.IsAny<DateTime>()), Times.AtLeastOnce);
        _mockRegistry.Verify(x => x.RemoveIfUnchanged(staleConsumer2, It.IsAny<DateTime>()), Times.AtLeastOnce);
        _mockRegistry.Verify(x => x.RemoveIfUnchanged(staleConsumer3, It.IsAny<DateTime>()), Times.AtLeastOnce);

        // Active consumer should NOT be unregistered
        _mockRegistry.Verify(x => x.RemoveIfUnchanged(activeConsumer, It.IsAny<DateTime>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldStopGracefully_WhenCancellationRequested()
    {
        // Arrange
        var consumers = new Dictionary<Guid, DateTime>().ToFrozenDictionary();

        _mockRegistry
            .Setup(x => x.GetRegisteredAsync())
            .ReturnsAsync(consumers);

        var worker = new ConsumerCleanupWorker(_mockRegistry.Object);

        using var cts = new CancellationTokenSource();

        // Act
        var executeTask = worker.StartAsync(cts.Token);
        await Task.Delay(500);
        cts.Cancel();

        // Assert - should complete without throwing
        var act = async () => await executeTask;
        await act.Should().NotThrowAsync();
    }
}
