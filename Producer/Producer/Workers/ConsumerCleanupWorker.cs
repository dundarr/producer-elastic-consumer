using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Producer.Services;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Producer.Workers;

/// <summary>
/// Background service that periodically cleans up stale consumer registrations.
/// Removes consumers that haven't sent a heartbeat in the last 10 seconds.
/// </summary>
public class ConsumerCleanupWorker : BackgroundService
{
    private readonly IConsumerRegistry _registry;
    private readonly ILogger<ConsumerCleanupWorker> _logger;

    public ConsumerCleanupWorker(IConsumerRegistry registry, ILogger<ConsumerCleanupWorker> logger)
    {
        _registry = registry;
        _logger = logger;
    }

    /// <summary>
    /// Runs every 10 seconds to identify and remove inactive consumers.
    /// A consumer is considered inactive if last heartbeat is older than 10 seconds.
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            // Get all registered consumers with their last heartbeat timestamp
            var dict = await _registry.GetRegisteredAsync();

            // Find consumers with heartbeat older than 10 seconds
            var cleanup = dict.Where(c => c.Value < (DateTime.Now - TimeSpan.FromSeconds(10)))
                              .Select(c => c.Key)
                              .ToList();

            // Unregister each stale consumer
            foreach (var e in cleanup)
            {
                await _registry.UnregisterAsync(e);
            }

            // Check every 10 seconds
            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
        }
    }
}
