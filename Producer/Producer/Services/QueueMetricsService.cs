using Azure.Storage.Queues;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace Producer.Services;

/// <summary>
/// Provides metrics and monitoring capabilities for the Azure Queue Storage.
/// Calculates queue length and consumption/production speed.
/// </summary>
public class QueueMetricsService : IQueueMetricsService
{
    private readonly QueueClient _queueClient;
    private readonly ILogger<QueueMetricsService> _logger;
    
    // Lazy initialization flag to ensure queue is created only once
    private Task? _ensureQueueTask;
    private readonly object _lock = new object();

    public QueueMetricsService(IConfiguration config, ILogger<QueueMetricsService> logger)
    {
        _queueClient = new QueueClient(
            config["Queue:ConnectionString"],
            config["Queue:QueueName"]
        );

        _logger = logger;
    }

    /// <summary>
    /// Ensures the queue exists, creating it if necessary.
    /// Uses lazy initialization pattern with thread-safety.
    /// </summary>
    private async Task EnsureQueueExistsAsync()
    {
        if (_ensureQueueTask != null)
        {
            await _ensureQueueTask.ConfigureAwait(false);
            return;
        }

        lock (_lock)
        {
            if (_ensureQueueTask != null)
            {
                // Another thread already created the task
            }
            else
            {
                _ensureQueueTask = CreateQueueIfNotExistsAsync();
            }
        }

        await _ensureQueueTask.ConfigureAwait(false);
    }

    /// <summary>
    /// Internal method to create the queue if it doesn't exist.
    /// </summary>
    private async Task CreateQueueIfNotExistsAsync()
    {
        try
        {
            await _queueClient.CreateIfNotExistsAsync().ConfigureAwait(false);
            _logger.LogInformation("Queue '{QueueName}' is ready for metrics.", _queueClient.Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create or verify queue '{QueueName}'.", _queueClient.Name);
            
            // Reset the task to allow retry on next call
            lock (_lock)
            {
                _ensureQueueTask = null;
            }
            
            throw;
        }
    }

    /// <summary>
    /// Gets the approximate number of messages currently in the queue.
    /// Note: Azure Queue Storage returns an approximate count, not exact.
    /// Creates the queue if it doesn't exist.
    /// </summary>
    public async Task<int> GetQueueLengthAsync()
    {
        try
        {
            // Ensure queue exists before reading properties
            await EnsureQueueExistsAsync().ConfigureAwait(false);
            
            var props = await _queueClient.GetPropertiesAsync().ConfigureAwait(false);
            return props.Value.ApproximateMessagesCount;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to read queue properties.");
            throw;
        }
    }

    /// <summary>
    /// Calculates the queue growth/consumption rate (messages per second).
    /// Measures queue length difference over 3 seconds.
    /// - Positive value: Queue is growing (production > consumption)
    /// - Negative value: Queue is shrinking (consumption > production)
    /// - Zero: Balanced (production = consumption)
    /// Creates the queue if it doesn't exist.
    /// </summary>
    public async Task<double> GetQueueSpeedAsync()
    {
        // Ensure queue exists before measuring
        await EnsureQueueExistsAsync().ConfigureAwait(false);
        
        // First measurement
        int first = await GetQueueLengthAsync().ConfigureAwait(false);

        // Wait 3 seconds
        await Task.Delay(TimeSpan.FromSeconds(3)).ConfigureAwait(false);

        // Second measurement
        int second = await GetQueueLengthAsync().ConfigureAwait(false);

        // Calculate delta (positive = growth, negative = consumption)
        int delta = second - first;
        
        // Convert to messages per second
        double speed = delta / 3.0;

        return speed;
    }
}
