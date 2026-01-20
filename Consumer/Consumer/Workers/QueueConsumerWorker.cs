using Azure.Storage.Queues;
using Consumer.Configuration;
using Consumer.Models;
using Consumer.Services;
using Microsoft.Extensions.Options;

namespace Consumer.Workers;

public class QueueConsumerWorker : BackgroundService
{
    private readonly ILogger<QueueConsumerWorker> _logger;
    private readonly IQueueClient _queueClient;
    private readonly TimeSpan _visibilityTimeout;
    private readonly IMessageProcessor _messageProcessor;

    // New variables for fixed-rate limiting
    private readonly double _fixedMessagesPerSecond;
    private readonly TimeSpan _minDelayBetweenStarts;
    private DateTimeOffset _nextAllowedStart = DateTimeOffset.MinValue;

    // Receive retry settings (moved from Polly to explicit logic)
    private readonly int _receiveMaxRetries;
    private readonly TimeSpan _receiveBaseDelay;
    private readonly ReceiveRetryPolicy _receiveRetryPolicy;

    // Dead-letter threshold configurable via QueueSettings
    private readonly int _deadLetterThreshold;

    public QueueConsumerWorker(
        ILogger<QueueConsumerWorker> logger,
        IOptions<QueueSettings> options,
        IMessageProcessor messageProcessor)
    {
        _logger = logger;

        var settings = options.Value;

        // default production client
        var native = new QueueClient(settings.ConnectionString, settings.Name);
        _queueClient = new AzureQueueClientWrapper(native);
        _queueClient.CreateIfNotExists();

        _visibilityTimeout = TimeSpan.FromSeconds(settings.VisibilityTimeoutSeconds);

        // Read new configuration and prepare the minimum interval between starts
        _fixedMessagesPerSecond = settings.FixedConsumptionRatePerSecond;
        _minDelayBetweenStarts = _fixedMessagesPerSecond > 0
            ? TimeSpan.FromSeconds(1.0 / _fixedMessagesPerSecond)
            : TimeSpan.Zero;

        _messageProcessor = messageProcessor;

        // read receive retry configuration with sensible defaults
        _receiveMaxRetries = settings.ReceiveMaxRetries > 0 ? settings.ReceiveMaxRetries : 5;
        _receiveBaseDelay = TimeSpan.FromMilliseconds(settings.ReceiveBaseDelayMs > 0 ? settings.ReceiveBaseDelayMs : 200);

        // instantiate the retry helper used to receive messages
        _receiveRetryPolicy = new ReceiveRetryPolicy(_receiveMaxRetries, _receiveBaseDelay);

        // dead-letter threshold taken from settings (fallback to 5 if invalid)
        _deadLetterThreshold = settings.DeadLetterThreshold > 0 ? settings.DeadLetterThreshold : 5;
    }

    // Overload for unit testing to inject a fake IQueueClient
    internal QueueConsumerWorker(
        ILogger<QueueConsumerWorker> logger,
        IOptions<QueueSettings> options,
        IMessageProcessor messageProcessor,
        IQueueClient queueClient)
        : this(logger, options, messageProcessor)
    {
        _queueClient = queueClient;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Consumer started (single-message mode)");

        while (!stoppingToken.IsCancellationRequested)
        {
            // Apply wait if a fixed rate has been configured by delegating to helper method
            if (_minDelayBetweenStarts > TimeSpan.Zero)
            {
                var now = DateTimeOffset.UtcNow;
                var wait = CalculateWaitForNextStart(now);

                if (wait > TimeSpan.Zero)
                    await Task.Delay(wait, stoppingToken);
            }

            SimpleQueueMessage? message;
            try
            {
                // Use the centralized retry helper to receive a message with backoff + jitter
                message = await ReceiveMessageWithRetryAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // orderly shutdown
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error receiving message after retries");
                await Task.Delay(500, stoppingToken);
                continue;
            }

            if (message == null)
            {
                await Task.Delay(200, stoppingToken);
                continue;
            }

            // Process sequentially: await completion before receiving next
            await ProcessMessage(message, stoppingToken);
        }
    }

    // Internal for testing: compute wait and reserve next slot based on configured rate.
    public TimeSpan CalculateWaitForNextStart(DateTimeOffset now)
    {
        TimeSpan wait;

        if (_nextAllowedStart == DateTimeOffset.MinValue)
        {
            // first time: allow immediately and reserve the next slot
            _nextAllowedStart = now.Add(_minDelayBetweenStarts);
            wait = TimeSpan.Zero;
        }
        else
        {
            if (now < _nextAllowedStart)
            {
                // We're ahead of schedule: wait until next allowed start and reserve the following slot
                wait = _nextAllowedStart - now;
                _nextAllowedStart = _nextAllowedStart.Add(_minDelayBetweenStarts);
            }
            else
            {
                // We're late or on time: start immediately and set next allowed start relative to now
                wait = TimeSpan.Zero;
                _nextAllowedStart = now.Add(_minDelayBetweenStarts);
            }
        }

        return wait;
    }

    private Task<SimpleQueueMessage?> ReceiveMessageWithRetryAsync(CancellationToken ct)
    {
        // Delegate to the reusable retry helper. The lambda adapts the QueueClient call signature.
        return _receiveRetryPolicy.ExecuteAsync(
            async cancellationToken =>
            {
                return await _queueClient.ReceiveMessageAsync(_visibilityTimeout, cancellationToken);
            },
            ct);
    }

    // made internal for unit testing and use SimpleQueueMessage abstraction
    internal async Task ProcessMessage(SimpleQueueMessage message, CancellationToken ct)
    {
        try
        {
            using var scope = _logger.BeginScope(new Dictionary<string, object>
            {
                ["MessageId"] = message.MessageId,
                ["PopReceipt"] = message.PopReceipt
            });

            _logger.LogInformation("Processing message {MessageId}", message.MessageId);

            // Delegate actual business processing to IMessageProcessor for testability.
            // The processor should honor the cancellation token.
            bool success = await _messageProcessor.ProcessAsync(message.MessageText, ct);

            if (success)
            {
                await _queueClient.DeleteMessageAsync(message.MessageId, message.PopReceipt, ct);
                _logger.LogInformation("Deleted message {MessageId}", message.MessageId);
            }
            else
            {
                if (message.DequeueCount >= _deadLetterThreshold)
                {
                    _logger.LogWarning("Message {MessageId} reached DequeueCount {DequeueCount} -> moving to DLQ",
                        message.MessageId, message.DequeueCount);

                    // If a DLQ client was injected, send to it and delete original (not shown here).
                    // await _deadLetterQueueClient.SendMessageAsync(message.MessageText, ct);
                    await _queueClient.DeleteMessageAsync(message.MessageId, message.PopReceipt, ct);
                }
                else
                {
                    _logger.LogInformation("Processing failed for {MessageId}, will become visible again", message.MessageId);
                }
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            _logger.LogInformation("Processing canceled for {MessageId}", message.MessageId);
            await _queueClient.DeleteMessageAsync(message.MessageId, message.PopReceipt, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled error while processing {MessageId}", message.MessageId);
            await _queueClient.DeleteMessageAsync(message.MessageId, message.PopReceipt, ct);
        }
    }
}