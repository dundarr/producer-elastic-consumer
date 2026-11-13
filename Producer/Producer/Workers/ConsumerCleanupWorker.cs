using Microsoft.Extensions.Hosting;
using Producer.Services;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Producer.Workers;

public class ConsumerCleanupWorker(IConsumerRegistry registry) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(10));
        do
        {
            var cutoff = DateTime.UtcNow - TimeSpan.FromSeconds(30);
            foreach (var (id, heartbeat) in await registry.GetRegisteredAsync())
            {
                if (heartbeat < cutoff)
                    registry.RemoveIfUnchanged(id, heartbeat);
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
