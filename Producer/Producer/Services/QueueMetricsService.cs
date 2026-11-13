using Azure;
using Azure.Storage.Queues;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Producer.Services;

public class QueueMetricsService(QueueClient queueClient, ILogger<QueueMetricsService> logger) : IQueueMetricsService
{
    private readonly SemaphoreSlim _queueCreation = new(1);
    private volatile bool _queueReady;

    public QueueMetricsService(IConfiguration configuration, ILogger<QueueMetricsService> logger)
        : this(new QueueClient(configuration["Queue:ConnectionString"], configuration["Queue:QueueName"]), logger)
    {
    }

    public async Task<int> GetQueueLengthAsync()
    {
        try
        {
            await EnsureQueueAsync();
            var properties = await queueClient.GetPropertiesAsync();
            return properties.Value.ApproximateMessagesCount;
        }
        catch (RequestFailedException ex) when (ex.ErrorCode == "QueueNotFound")
        {
            _queueReady = false;
            await EnsureQueueAsync();
            var properties = await queueClient.GetPropertiesAsync();
            return properties.Value.ApproximateMessagesCount;
        }
    }

    public async Task<double> GetQueueSpeedAsync()
    {
        var first = await GetQueueLengthAsync();
        var started = Stopwatch.GetTimestamp();
        await Task.Delay(TimeSpan.FromSeconds(3));
        var second = await GetQueueLengthAsync();
        return (second - first) / Stopwatch.GetElapsedTime(started).TotalSeconds;
    }

    private async Task EnsureQueueAsync()
    {
        if (_queueReady)
            return;

        await _queueCreation.WaitAsync();
        try
        {
            if (!_queueReady)
            {
                await queueClient.CreateIfNotExistsAsync();
                _queueReady = true;
                logger.LogInformation("Queue {QueueName} is ready for metrics", queueClient.Name);
            }
        }
        finally
        {
            _queueCreation.Release();
        }
    }
}
