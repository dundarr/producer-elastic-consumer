using Azure.Storage.Queues;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Producer.Services;
using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Producer.Workers;

/// <summary>
/// Background service responsible for producing and sending messages to Azure Queue Storage.
/// Runs continuously in the background and respects the control state (running/paused).
/// </summary>
public class ProducerWorker : BackgroundService
{
    private readonly QueueClient _queueClient;
    private readonly ILogger<ProducerWorker> _logger;
    private readonly IProducerWorkerControl _control;
    private readonly IQueueSendPolicy _sendPolicy;

    // Async-lazy task used to ensure the queue exists exactly once (with retry on failure)
    // This prevents multiple concurrent queue creation attempts
    private Task? _ensureTask;

    public ProducerWorker(QueueClient queueClient, ILogger<ProducerWorker> logger, IProducerWorkerControl control, IQueueSendPolicy sendPolicy)
    {
        _queueClient = queueClient;
        _logger = logger;
        _control = control;
        _sendPolicy = sendPolicy;
    }

    /// <summary>
    /// Main execution loop - runs continuously until the service stops.
    /// Checks control state and sends messages according to the configured rate.
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ProducerWorker started (in paused mode).");

        while (!stoppingToken.IsCancellationRequested)
        {
            // Check if production is enabled and get current rate
            bool running = _control.IsRunning();
            int rate = _control.GetRate();

            // If paused, wait and check again
            if (!running)
            {
                await Task.Delay(250, stoppingToken);
                continue;
            }

            // Send 'rate' number of messages per iteration
            for (int i = 0; i < rate && !stoppingToken.IsCancellationRequested; i++)
            {
                var id = Guid.NewGuid();
                try
                {
                    // Ensure queue exists before sending (lazy initialization with thread-safety)
                    await EnsureQueueAsync(stoppingToken).ConfigureAwait(false);
                    _logger.LogInformation("Sending message {MessageId}", id);

                    // Wrap send operation in resilience pipeline (retry + timeout)
                    await _sendPolicy.ExecuteAsync(async ct =>
                    {
                        var json_message = JsonSerializer.Serialize<string>($"msg-{id}");
                        await _queueClient.SendMessageAsync(json_message, cancellationToken: ct).ConfigureAwait(false);
                    }, stoppingToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    // Graceful shutdown requested
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error sending message to queue (messageId: {MessageId}).", id);
                }
            }

            // Wait 1 second before sending next batch (implements "messages per second")
            await Task.Delay(1000, stoppingToken);
        }

        _logger.LogInformation("ProducerWorker stopped.");
    }

    /// <summary>
    /// Ensures the queue exists, using a lazy initialization pattern with thread-safety.
    /// Multiple concurrent calls will result in only one actual queue creation attempt.
    /// </summary>
    private async Task EnsureQueueAsync(CancellationToken cancellationToken)
    {
        var existing = _ensureTask;
        if (existing != null)
        {
            // Queue creation already in progress or completed, just await it
            await existing.ConfigureAwait(false);
            return;
        }

        // Create the ensure task
        Task CreateEnsureTask() => InternalEnsureQueueAsync(cancellationToken);

        var newTask = CreateEnsureTask();
        
        // Atomically set _ensureTask if it's still null
        // CompareExchange returns the original value - if null, our task was set
        var original = System.Threading.Interlocked.CompareExchange(ref _ensureTask, newTask, null);
        
        if (original == null)
        {
            // We won the race - execute our task
            try
            {
                await newTask.ConfigureAwait(false);
            }
            catch
            {
                // On failure, reset _ensureTask to allow retry
                await System.Threading.Interlocked.Exchange(ref _ensureTask, null);
                throw;
            }
        }
        else
        {
            // Another thread won the race - await their task
            await original.ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Internal method that actually creates the queue if it doesn't exist.
    /// </summary>
    private async Task InternalEnsureQueueAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _queueClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating or ensuring queue exists.");
            throw;
        }
    }
}
