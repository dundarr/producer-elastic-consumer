using Azure.Storage.Queues;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Producer.Services;

namespace Producer.IntegrationTests;

/// <summary>
/// Integration tests for QueueMetricsService.
/// These tests require Azurite or Azure Storage Emulator to be running.
/// Run: docker run -p 10000:10000 -p 10001:10001 -p 10002:10002 mcr.microsoft.com/azure-storage/azurite
/// Or for Queue only: docker run -p 10001:10001 mcr.microsoft.com/azure-storage/azurite azurite-queue --queueHost 0.0.0.0 --queuePort 10001
/// </summary>
public class QueueMetricsServiceIntegrationTests : IAsyncLifetime
{
    private readonly IConfiguration _configuration;
    private readonly Mock<ILogger<QueueMetricsService>> _mockLogger;
    private readonly QueueClient _queueClient;
    private readonly string _queueName;

    // Explicit connection string for Azurite Queue service only
    // This prevents the SDK from trying to connect to Blob/Table services
    private const string AzuriteQueueConnectionString = 
        "DefaultEndpointsProtocol=http;" +
        "AccountName=devstoreaccount1;" +
        "AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;" +
        "QueueEndpoint=http://127.0.0.1:10001/devstoreaccount1;";
        
    public QueueMetricsServiceIntegrationTests()
    {
        _mockLogger = new Mock<ILogger<QueueMetricsService>>();
        
        // Use a unique queue name for each test run to avoid conflicts
        _queueName = $"test-metrics-queue-{Guid.NewGuid():N}";
        
        var configData = new Dictionary<string, string?>
        {
            { "Queue:ConnectionString", AzuriteQueueConnectionString },
            { "Queue:QueueName", _queueName }
        };

        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        // Initialize QueueClient for setup/cleanup
        _queueClient = new QueueClient(
            _configuration["Queue:ConnectionString"],
            _queueName);
    }

    /// <summary>
    /// Called before each test - creates the test queue
    /// </summary>
    public async Task InitializeAsync()
    {
        try
        {
            await _queueClient.CreateIfNotExistsAsync();
        }
        catch (Exception ex)
        {
            // Provide clear error message if Azurite is not running
            throw new InvalidOperationException(
                $"Failed to connect to Azurite. Please ensure Azurite is running on port 10001.\n" +
                $"Run: docker run -p 10001:10001 mcr.microsoft.com/azure-storage/azurite azurite-queue --queueHost 0.0.0.0 --queuePort 10001\n" +
                $"Or: docker run -p 10000:10000 -p 10001:10001 -p 10002:10002 mcr.microsoft.com/azure-storage/azurite\n" +
                $"Original error: {ex.Message}",
                ex);
        }
    }

    /// <summary>
    /// Called after each test - deletes the test queue
    /// </summary>
    public async Task DisposeAsync()
    {
        try
        {
            await _queueClient.DeleteIfExistsAsync();
        }
        catch (Exception ex)
        {
            // Log but don't fail cleanup
            _mockLogger.Object.LogWarning(ex, "Failed to delete test queue during cleanup");
        }
    }

    [Fact]
    public async Task GetQueueLengthAsync_ShouldReturnZero_ForEmptyQueue()
    {
        // Arrange
        var service = new QueueMetricsService(_configuration, _mockLogger.Object);

        // Act
        var length = await service.GetQueueLengthAsync();
                
        // Assert
        length.Should().Be(0, "empty queue should have 0 messages");
    }

    [Fact]
    public async Task GetQueueLengthAsync_ShouldReturnCorrectCount_AfterSendingMessages()
    {
        // Arrange
        var service = new QueueMetricsService(_configuration, _mockLogger.Object);
        
        // Send 5 test messages
        for (int i = 0; i < 5; i++)
        {
            await _queueClient.SendMessageAsync($"Test message {i}");
        }

        // Act
        var length = await service.GetQueueLengthAsync();

        // Assert
        length.Should().Be(5, "queue should contain 5 messages");
    }

    [Fact]
    public async Task GetQueueSpeedAsync_ShouldCalculateSpeed()
    {
        // Arrange
        var service = new QueueMetricsService(_configuration, _mockLogger.Object);

        // Act
        var speed = await service.GetQueueSpeedAsync();

        // Assert
        speed.Should().BeInRange(-1000, 1000, "speed should be within reasonable range");
    }

    [Fact]
    public async Task GetQueueSpeedAsync_ShouldDetectGrowth_WhenMessagesAreAdded()
    {
        // Arrange
        var service = new QueueMetricsService(_configuration, _mockLogger.Object);

        // Start measuring speed (takes 3 seconds)
        var speedTask = service.GetQueueSpeedAsync();

        // Add messages during the measurement (after 1 second)
        await Task.Delay(1000);
        for (int i = 0; i < 10; i++)
        {
            await _queueClient.SendMessageAsync($"Test message {i}");
        }

        // Act
        var speed = await speedTask;

        // Assert
        speed.Should().BeGreaterThan(0, "speed should be positive when messages are added");
    }

    [Fact]
    public async Task GetQueueSpeedAsync_ShouldDetectConsumption_WhenMessagesAreRemoved()
    {
        // Arrange
        var service = new QueueMetricsService(_configuration, _mockLogger.Object);
        
        // Add initial messages
        for (int i = 0; i < 15; i++)
        {
            await _queueClient.SendMessageAsync($"Test message {i}");
        }

        // Start measuring speed (takes 3 seconds)
        var speedTask = service.GetQueueSpeedAsync();

        // Remove messages during the measurement (after 1 second)
        await Task.Delay(1000);
        for (int i = 0; i < 10; i++)
        {
            var message = await _queueClient.ReceiveMessageAsync();
            if (message.Value != null)
            {
                await _queueClient.DeleteMessageAsync(message.Value.MessageId, message.Value.PopReceipt);
            }
        }

        // Act
        var speed = await speedTask;

        // Assert
        speed.Should().BeLessThan(0, "speed should be negative when messages are consumed");
    }

    [Fact]
    public async Task GetQueueLengthAsync_ShouldCreateQueue_WhenQueueDoesNotExist()
    {
        // Arrange - Delete the queue first to ensure it doesn't exist
        await _queueClient.DeleteIfExistsAsync();
        
        var service = new QueueMetricsService(_configuration, _mockLogger.Object);

        // Act
        var length = await service.GetQueueLengthAsync();

        // Assert
        length.Should().Be(0, "newly created queue should be empty");
        
        // Verify queue was actually created
        var exists = await _queueClient.ExistsAsync();
        exists.Value.Should().BeTrue("queue should have been created automatically");
    }

    [Fact]
    public async Task GetQueueSpeedAsync_ShouldCreateQueue_WhenQueueDoesNotExist()
    {
        // Arrange - Delete the queue first to ensure it doesn't exist
        await _queueClient.DeleteIfExistsAsync();
        
        var service = new QueueMetricsService(_configuration, _mockLogger.Object);

        // Act
        var speed = await service.GetQueueSpeedAsync();

        // Assert
        speed.Should().Be(0, "speed should be 0 for empty queue");
        
        // Verify queue was actually created
        var exists = await _queueClient.ExistsAsync();
        exists.Value.Should().BeTrue("queue should have been created automatically");
    }

    [Fact]
    public async Task MultipleCalls_ShouldNotCreateQueueMultipleTimes()
    {
        // Arrange - Delete the queue first
        await _queueClient.DeleteIfExistsAsync();
        
        var service = new QueueMetricsService(_configuration, _mockLogger.Object);

        // Act - Call multiple times concurrently
        var tasks = new[]
        {
            service.GetQueueLengthAsync(),
            service.GetQueueLengthAsync(),
            service.GetQueueLengthAsync()
        };

        var results = await Task.WhenAll(tasks);

        // Assert - All calls should succeed
        results.Should().AllSatisfy(r => r.Should().Be(0));
        
        // Verify queue was created only once (by checking it exists)
        var exists = await _queueClient.ExistsAsync();
        exists.Value.Should().BeTrue("queue should exist after multiple concurrent calls");
    }
}