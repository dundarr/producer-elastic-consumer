using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Producer.Services;

/// <summary>
/// Implements resilience policy for sending messages to Azure Queue Storage.
/// Provides automatic retry with exponential backoff and timeout protection.
/// </summary>
public class QueueSendPolicy : IQueueSendPolicy
{
    private readonly ILogger<QueueSendPolicy> _logger;
    private readonly int _maxRetries = 3;
    private readonly TimeSpan _baseDelay = TimeSpan.FromSeconds(2);
    private readonly TimeSpan _timeout = TimeSpan.FromSeconds(10);

    public QueueSendPolicy(ILogger<QueueSendPolicy> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Executes the provided action with retry and timeout policies.
    /// - Timeout: 10 seconds per attempt
    /// - Retries: Up to 3 attempts with exponential backoff (2s, 4s, 8s)
    /// </summary>
    public async Task ExecuteAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken)
    {
        int attempt = 0;

        while (true)
        {
            attempt++;
            try
            {
                // Create a timeout token for this attempt
                using var timeoutCts = new CancellationTokenSource(_timeout);
                
                // Combine the outer cancellation token with the timeout token
                using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
                
                // Execute the action with combined cancellation token
                await action(linked.Token).ConfigureAwait(false);
                return; // Success - exit retry loop
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // If outer cancellation requested (service stopping), propagate immediately
                throw;
            }
            catch (Exception ex)
            {
                // Check if we've exhausted retries
                if (attempt > _maxRetries)
                {
                    _logger.LogError(ex, "Send failed after {Attempt} attempts.", attempt - 1);
                    throw;
                }

                // Calculate exponential backoff delay: 2^(attempt-1) * baseDelay
                // Attempt 1: 2s, Attempt 2: 4s, Attempt 3: 8s
                var delaySeconds = Math.Pow(2, attempt - 1) * _baseDelay.TotalSeconds;
                var delay = TimeSpan.FromSeconds(delaySeconds);
                
                _logger.LogWarning(ex, "Send attempt {Attempt} failed. Retrying after {Delay}.", attempt, delay);
                
                try
                {
                    await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    // Service stopping during retry delay
                    throw;
                }
            }
        }
    }
}