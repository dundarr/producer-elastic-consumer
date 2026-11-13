using Azure;
using Azure.Storage.Queues;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Producer.Services;
using System;
using System.Diagnostics;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Producer.Workers;

public class ProducerWorker(
    QueueClient queueClient,
    ILogger<ProducerWorker> logger,
    IProducerWorkerControl control,
    IQueueSendPolicy sendPolicy) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var queueReady = false;

        while (!stoppingToken.IsCancellationRequested)
        {
            if (!control.IsRunning() || control.GetRate() <= 0)
            {
                await Task.Delay(100, stoppingToken);
                continue;
            }

            var started = Stopwatch.GetTimestamp();
            try
            {
                if (!queueReady)
                {
                    await queueClient.CreateIfNotExistsAsync(cancellationToken: stoppingToken);
                    queueReady = true;
                }

                if (!control.IsRunning())
                    continue;

                var payload = JsonSerializer.Serialize($"msg-{Guid.NewGuid()}");
                await sendPolicy.ExecuteAsync(async ct =>
                {
                    await queueClient.SendMessageAsync(payload, cancellationToken: ct);
                }, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                if (ex is RequestFailedException { ErrorCode: "QueueNotFound" })
                    queueReady = false;

                logger.LogError(ex, "Could not enqueue a message. Retrying");
                await Task.Delay(250, stoppingToken);
                continue;
            }

            var interval = TimeSpan.FromSeconds(1.0 / Math.Max(1, control.GetRate()));
            var remaining = interval - Stopwatch.GetElapsedTime(started);
            if (remaining > TimeSpan.Zero)
                await Task.Delay(remaining, stoppingToken);
        }
    }
}
