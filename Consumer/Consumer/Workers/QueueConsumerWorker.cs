using Azure;
using Consumer.Configuration;
using Consumer.Models;
using Consumer.Services;
using Microsoft.Extensions.Options;

namespace Consumer.Workers;

public class QueueConsumerWorker(
    ILogger<QueueConsumerWorker> logger,
    IOptions<QueueSettings> options,
    IMessageProcessor messageProcessor,
    IQueueClient queueClient,
    [FromKeyedServices("dead-letter")] IQueueClient deadLetterQueue) : BackgroundService
{
    private readonly TimeSpan _visibilityTimeout = TimeSpan.FromSeconds(options.Value.VisibilityTimeoutSeconds);
    private readonly TimeSpan _minDelayBetweenStarts = options.Value.FixedConsumptionRatePerSecond > 0
        ? TimeSpan.FromSeconds(1.0 / options.Value.FixedConsumptionRatePerSecond)
        : TimeSpan.Zero;
    private readonly ReceiveRetryPolicy _receiveRetry = new(
        options.Value.ReceiveMaxRetries > 0 ? options.Value.ReceiveMaxRetries : 5,
        TimeSpan.FromMilliseconds(options.Value.ReceiveBaseDelayMs > 0 ? options.Value.ReceiveBaseDelayMs : 200));
    private readonly int _deadLetterThreshold = options.Value.DeadLetterThreshold > 0
        ? options.Value.DeadLetterThreshold
        : 5;
    private DateTimeOffset _nextAllowedStart = DateTimeOffset.MinValue;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Consumer started (single-message mode)");
        var queueReady = false;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (!queueReady)
                {
                    await queueClient.CreateIfNotExistsAsync(stoppingToken);
                    queueReady = true;
                }

                var wait = CalculateWaitForNextStart(DateTimeOffset.UtcNow);
                if (wait > TimeSpan.Zero)
                    await Task.Delay(wait, stoppingToken);

                var message = await _receiveRetry.ExecuteAsync(
                    ct => queueClient.ReceiveMessageAsync(_visibilityTimeout, ct), stoppingToken);

                if (message is null)
                    await Task.Delay(200, stoppingToken);
                else
                    await ProcessMessage(message, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                if (ex is RequestFailedException { ErrorCode: "QueueNotFound" })
                    queueReady = false;

                logger.LogWarning(ex, "Could not read the queue. Retrying");
                await Task.Delay(500, stoppingToken);
            }
        }
    }

    public TimeSpan CalculateWaitForNextStart(DateTimeOffset now)
    {
        var start = now < _nextAllowedStart ? _nextAllowedStart : now;
        _nextAllowedStart = start + _minDelayBetweenStarts;
        return start - now;
    }

    internal async Task ProcessMessage(SimpleQueueMessage message, CancellationToken ct)
    {
        try
        {
            ct.ThrowIfCancellationRequested();
            var processed = await messageProcessor.ProcessAsync(message.MessageText, ct);
            if (!processed)
            {
                if (message.DequeueCount < _deadLetterThreshold)
                    return;

                await deadLetterQueue.CreateIfNotExistsAsync(ct);
                await deadLetterQueue.SendMessageAsync(message.MessageText, ct);
                logger.LogWarning("Moved message {MessageId} to the dead-letter queue after {Attempts} attempts",
                    message.MessageId, message.DequeueCount);
            }

            ct.ThrowIfCancellationRequested();
            await queueClient.DeleteMessageAsync(message.MessageId, message.PopReceipt, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not complete message {MessageId}. It can be retried when visible again",
                message.MessageId);
        }
    }
}
