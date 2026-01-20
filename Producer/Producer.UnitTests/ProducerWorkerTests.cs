using Azure;
using Azure.Storage.Queues;
using Azure.Storage.Queues.Models;
using Microsoft.Extensions.Logging;
using Producer.Services;
using Producer.Workers;

namespace Producer.UnitTests;

/// <summary>
/// Unit tests for ProducerWorker - validates background message production logic.
/// </summary>
public class ProducerWorkerTests
{
    private readonly Mock<QueueClient> _mockQueueClient;
    private readonly Mock<ILogger<ProducerWorker>> _mockLogger;
    private readonly Mock<IProducerWorkerControl> _mockControl;
    private readonly Mock<IQueueSendPolicy> _mockSendPolicy;

    public ProducerWorkerTests()
    {
        // QueueClient requires Uri and QueueClientOptions in constructor
        var uri = new Uri("https://test.queue.core.windows.net/testqueue");
        var options = new QueueClientOptions();
        
        _mockQueueClient = new Mock<QueueClient>(MockBehavior.Loose, uri, options);
        
        _mockLogger = new Mock<ILogger<ProducerWorker>>();
        _mockControl = new Mock<IProducerWorkerControl>();
        _mockSendPolicy = new Mock<IQueueSendPolicy>();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldNotSendMessages_WhenProducerIsStopped()
    {
        // Arrange
        _mockControl.Setup(x => x.IsRunning()).Returns(false);
        
        var worker = new ProducerWorker(
            _mockQueueClient.Object,
            _mockLogger.Object,
            _mockControl.Object,
            _mockSendPolicy.Object);

        using var cts = new CancellationTokenSource();
        
        // Act
        var executeTask = worker.StartAsync(cts.Token);
        await Task.Delay(500); // Let it run for a bit
        cts.Cancel();
        
        try
        {
            await executeTask;
        }
        catch (OperationCanceledException)
        {
            // Expected
        }

        // Assert - SendPolicy should not be called when stopped
        _mockSendPolicy.Verify(
            x => x.ExecuteAsync(It.IsAny<Func<CancellationToken, Task>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldSendMessages_WhenProducerIsRunning()
    {
        // Arrange
        _mockControl.Setup(x => x.IsRunning()).Returns(true);
        _mockControl.Setup(x => x.GetRate()).Returns(1);
        
        // Mock CreateIfNotExistsAsync - returns Response (not generic)
        _mockQueueClient
            .Setup(x => x.CreateIfNotExistsAsync(
                It.IsAny<IDictionary<string, string>>(), 
                It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult(Mock.Of<Response>()));

        // Mock SendMessageAsync
        _mockQueueClient
            .Setup(x => x.SendMessageAsync(
                It.IsAny<string>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Mock.Of<Response<SendReceipt>>());

        // Mock the send policy to execute the action immediately
        _mockSendPolicy
            .Setup(x => x.ExecuteAsync(
                It.IsAny<Func<CancellationToken, Task>>(), 
                It.IsAny<CancellationToken>()))
            .Returns<Func<CancellationToken, Task>, CancellationToken>((action, ct) => action(ct));

        var worker = new ProducerWorker(
            _mockQueueClient.Object,
            _mockLogger.Object,
            _mockControl.Object,
            _mockSendPolicy.Object);

        using var cts = new CancellationTokenSource();

        // Act
        var executeTask = worker.StartAsync(cts.Token);
        await Task.Delay(1500); // Let it run for at least one iteration
        cts.Cancel();
        
        try
        {
            await executeTask;
        }
        catch (OperationCanceledException)
        {
            // Expected
        }

        // Assert - SendPolicy should be called at least once
        _mockSendPolicy.Verify(
            x => x.ExecuteAsync(
                It.IsAny<Func<CancellationToken, Task>>(), 
                It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldRespectRate_WhenSendingMessages()
    {
        // Arrange
        var messagesPerSecond = 3;
        _mockControl.Setup(x => x.IsRunning()).Returns(true);
        _mockControl.Setup(x => x.GetRate()).Returns(messagesPerSecond);

        _mockQueueClient
            .Setup(x => x.CreateIfNotExistsAsync(
                It.IsAny<IDictionary<string, string>>(), 
                It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult(Mock.Of<Response>()));

        _mockQueueClient
            .Setup(x => x.SendMessageAsync(
                It.IsAny<string>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Mock.Of<Response<SendReceipt>>());

        var callCount = 0;
        _mockSendPolicy
            .Setup(x => x.ExecuteAsync(
                It.IsAny<Func<CancellationToken, Task>>(), 
                It.IsAny<CancellationToken>()))
            .Callback(() => callCount++)
            .Returns<Func<CancellationToken, Task>, CancellationToken>((action, ct) => action(ct));

        var worker = new ProducerWorker(
            _mockQueueClient.Object,
            _mockLogger.Object,
            _mockControl.Object,
            _mockSendPolicy.Object);

        using var cts = new CancellationTokenSource();

        // Act
        var executeTask = worker.StartAsync(cts.Token);
        await Task.Delay(1500); // Let it run for one full iteration
        cts.Cancel();
        
        try
        {
            await executeTask;
        }
        catch (OperationCanceledException)
        {
            // Expected
        }

        // Assert - should send 3 messages per iteration
        callCount.Should().BeGreaterThanOrEqualTo(messagesPerSecond);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldHandleExceptions_AndContinueRunning()
    {
        // Arrange
        _mockControl.Setup(x => x.IsRunning()).Returns(true);
        _mockControl.Setup(x => x.GetRate()).Returns(1);

        _mockQueueClient
            .Setup(x => x.CreateIfNotExistsAsync(
                It.IsAny<IDictionary<string, string>>(), 
                It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult(Mock.Of<Response>()));

        _mockQueueClient
            .Setup(x => x.SendMessageAsync(
                It.IsAny<string>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Mock.Of<Response<SendReceipt>>());

        var callCount = 0;
        _mockSendPolicy
            .Setup(x => x.ExecuteAsync(
                It.IsAny<Func<CancellationToken, Task>>(), 
                It.IsAny<CancellationToken>()))
            .Callback(() => callCount++)
            .Returns<Func<CancellationToken, Task>, CancellationToken>((action, ct) =>
            {
                // First call fails, subsequent calls succeed
                if (callCount == 1)
                    throw new InvalidOperationException("Simulated error");
                return action(ct);
            });

        var worker = new ProducerWorker(
            _mockQueueClient.Object,
            _mockLogger.Object,
            _mockControl.Object,
            _mockSendPolicy.Object);

        using var cts = new CancellationTokenSource();

        // Act
        var executeTask = worker.StartAsync(cts.Token);
        await Task.Delay(2500); // Let it run for multiple iterations
        cts.Cancel();
        
        try
        {
            await executeTask;
        }
        catch (OperationCanceledException)
        {
            // Expected
        }

        // Assert - should continue after exception
        callCount.Should().BeGreaterThan(1);
        
        // Verify error was logged
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldCreateQueueIfNotExists()
    {
        // Arrange
        _mockControl.Setup(x => x.IsRunning()).Returns(true);
        _mockControl.Setup(x => x.GetRate()).Returns(1);

        _mockQueueClient
            .Setup(x => x.CreateIfNotExistsAsync(
                It.IsAny<IDictionary<string, string>>(), 
                It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult(Mock.Of<Response>()));

        _mockQueueClient
            .Setup(x => x.SendMessageAsync(
                It.IsAny<string>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Mock.Of<Response<SendReceipt>>());

        _mockSendPolicy
            .Setup(x => x.ExecuteAsync(
                It.IsAny<Func<CancellationToken, Task>>(), 
                It.IsAny<CancellationToken>()))
            .Returns<Func<CancellationToken, Task>, CancellationToken>((action, ct) => action(ct));

        var worker = new ProducerWorker(
            _mockQueueClient.Object,
            _mockLogger.Object,
            _mockControl.Object,
            _mockSendPolicy.Object);

        using var cts = new CancellationTokenSource();

        // Act
        var executeTask = worker.StartAsync(cts.Token);
        await Task.Delay(1500);
        cts.Cancel();
        
        try
        {
            await executeTask;
        }
        catch (OperationCanceledException)
        {
            // Expected
        }

        // Assert - CreateIfNotExistsAsync should be called (lazy initialization)
        _mockQueueClient.Verify(
            x => x.CreateIfNotExistsAsync(
                It.IsAny<IDictionary<string, string>>(), 
                It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldStopGracefully_WhenCancellationRequested()
    {
        // Arrange
        _mockControl.Setup(x => x.IsRunning()).Returns(true);
        _mockControl.Setup(x => x.GetRate()).Returns(1);

        _mockQueueClient
            .Setup(x => x.CreateIfNotExistsAsync(
                It.IsAny<IDictionary<string, string>>(), 
                It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult(Mock.Of<Response>()));

        _mockQueueClient
            .Setup(x => x.SendMessageAsync(
                It.IsAny<string>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Mock.Of<Response<SendReceipt>>());

        _mockSendPolicy
            .Setup(x => x.ExecuteAsync(
                It.IsAny<Func<CancellationToken, Task>>(), 
                It.IsAny<CancellationToken>()))
            .Returns<Func<CancellationToken, Task>, CancellationToken>((action, ct) => action(ct));

        var worker = new ProducerWorker(
            _mockQueueClient.Object,
            _mockLogger.Object,
            _mockControl.Object,
            _mockSendPolicy.Object);

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