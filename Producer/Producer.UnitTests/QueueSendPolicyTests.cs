using Microsoft.Extensions.Logging;
using Producer.Services;

namespace Producer.UnitTests;

/// <summary>
/// Unit tests for QueueSendPolicy - validates retry logic and timeout behavior.
/// </summary>
public class QueueSendPolicyTests
{
    private readonly Mock<ILogger<QueueSendPolicy>> _mockLogger;

    public QueueSendPolicyTests()
    {
        _mockLogger = new Mock<ILogger<QueueSendPolicy>>();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldSucceed_WhenActionSucceedsOnFirstAttempt()
    {
        // Arrange
        var policy = new QueueSendPolicy(_mockLogger.Object);
        var executed = false;

        // Act
        await policy.ExecuteAsync(async ct =>
        {
            executed = true;
            await Task.CompletedTask;
        }, CancellationToken.None);

        // Assert
        executed.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldRetry_WhenActionFailsInitially()
    {
        // Arrange
        var policy = new QueueSendPolicy(_mockLogger.Object);
        var attemptCount = 0;

        // Act
        await policy.ExecuteAsync(async ct =>
        {
            attemptCount++;
            if (attemptCount < 2)
            {
                throw new InvalidOperationException("Simulated failure");
            }
            await Task.CompletedTask;
        }, CancellationToken.None);

        // Assert
        attemptCount.Should().Be(2);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldThrow_AfterMaxRetries()
    {
        // Arrange
        var policy = new QueueSendPolicy(_mockLogger.Object);
        var attemptCount = 0;

        // Act
        var act = async () => await policy.ExecuteAsync(async ct =>
        {
            attemptCount++;
            await Task.CompletedTask;
            throw new InvalidOperationException("Persistent failure");
        }, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
        attemptCount.Should().Be(4); // Initial attempt + 3 retries
    }

    [Fact]
    public async Task ExecuteAsync_ShouldRespectCancellation_WhenTokenCancelled()
    {
        // Arrange
        var policy = new QueueSendPolicy(_mockLogger.Object);
        var cts = new CancellationTokenSource();
        var executed = false;

        // Act
        var task = policy.ExecuteAsync(async ct =>
        {
            cts.Cancel(); // Cancel during execution
            ct.ThrowIfCancellationRequested();
            executed = true;
            await Task.CompletedTask;
        }, cts.Token);

        // Assert
        await Assert.ThrowsAsync<OperationCanceledException>(() => task);
        executed.Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldApplyTimeout()
    {
        // Arrange
        var policy = new QueueSendPolicy(_mockLogger.Object);

        // Act
        var act = async () => await policy.ExecuteAsync(async ct =>
        {
            // Simulate long-running operation (11 seconds, timeout is 10)
            await Task.Delay(TimeSpan.FromSeconds(11), ct);
        }, CancellationToken.None);

        // Assert - should timeout and retry, eventually failing
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldLogWarning_OnRetry()
    {
        // Arrange
        var policy = new QueueSendPolicy(_mockLogger.Object);
        var attemptCount = 0;

        // Act
        await policy.ExecuteAsync(async ct =>
        {
            attemptCount++;
            if (attemptCount < 2)
            {
                throw new InvalidOperationException("Simulated failure");
            }
            await Task.CompletedTask;
        }, CancellationToken.None);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldLogError_AfterMaxRetries()
    {
        // Arrange
        var policy = new QueueSendPolicy(_mockLogger.Object);

        // Act
        try
        {
            await policy.ExecuteAsync(async ct =>
            {
                await Task.CompletedTask;
                throw new InvalidOperationException("Persistent failure");
            }, CancellationToken.None);
        }
        catch
        {
            // Expected
        }

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldUseExponentialBackoff()
    {
        // Arrange
        var policy = new QueueSendPolicy(_mockLogger.Object);
        var attemptTimes = new List<DateTime>();

        // Act
        try
        {
            await policy.ExecuteAsync(async ct =>
            {
                attemptTimes.Add(DateTime.UtcNow);
                await Task.CompletedTask;
                throw new InvalidOperationException("Simulated failure");
            }, CancellationToken.None);
        }
        catch
        {
            // Expected to fail after retries
        }

        // Assert - verify exponential backoff (2s, 4s, 8s delays)
        attemptTimes.Should().HaveCount(4); // Initial + 3 retries
        
        if (attemptTimes.Count >= 2)
        {
            var firstDelay = (attemptTimes[1] - attemptTimes[0]).TotalSeconds;
            firstDelay.Should().BeGreaterThanOrEqualTo(1.9); // ~2 seconds
        }
    }
}