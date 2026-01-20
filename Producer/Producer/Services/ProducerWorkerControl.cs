using Microsoft.Extensions.Options;
using Producer.Configuration;
using System.Threading;

namespace Producer.Services;

/// <summary>
/// Controls the producer worker's execution state and message production rate.
/// Thread-safe implementation using volatile fields and Interlocked operations.
/// </summary>
public class ProducerWorkerControl : IProducerWorkerControl
{
    // volatile ensures visibility of changes across threads without full locks
    // This flag controls whether the producer is actively sending messages
    private volatile bool _running;
    
    // Thread-safe rate control - stores messages per second configuration
    private volatile int _messagesPerSecond = 1;

    /// <summary>
    /// Initializes the control with the current configuration and sets up
    /// a listener for configuration changes at runtime.
    /// </summary>
    public ProducerWorkerControl(IOptionsMonitor<QueueOptions> options)
    {
        // Get initial rate from configuration, defaulting to 1 if not set
        var initial = options?.CurrentValue?.MessagesPerSecond ?? 1;
        
        // Interlocked.Exchange ensures atomic read-modify-write operation
        // Math.Max ensures rate is always at least 1 (prevents zero or negative rates)
        Interlocked.Exchange(ref _messagesPerSecond, System.Math.Max(1, initial));

        // Register a callback for runtime configuration changes
        // This allows hot-reloading of appsettings.json without restarting the service
        options?.OnChange(o =>
        {
            // Thread-safe update when configuration changes
            Interlocked.Exchange(ref _messagesPerSecond, System.Math.Max(1, o.MessagesPerSecond));
        });
    }

    /// <summary>
    /// Starts the producer - allows ProducerWorker to send messages to the queue.
    /// </summary>
    public void StartProducing() => _running = true;

    /// <summary>
    /// Stops the producer - prevents ProducerWorker from sending new messages.
    /// </summary>
    public void StopProducing() => _running = false;

    /// <summary>
    /// Checks if the producer is currently running.
    /// Thread-safe due to volatile field.
    /// </summary>
    public bool IsRunning() => _running;

    /// <summary>
    /// Updates the message production rate at runtime.
    /// Thread-safe operation using Interlocked to prevent race conditions.
    /// </summary>
    /// <param name="rate">New messages per second rate (minimum value is 1)</param>
    public void SetRate(int rate) => Interlocked.Exchange(ref _messagesPerSecond, System.Math.Max(1, rate));

    /// <summary>
    /// Gets the current message production rate.
    /// Thread-safe due to volatile field.
    /// </summary>
    public int GetRate() => _messagesPerSecond;
}