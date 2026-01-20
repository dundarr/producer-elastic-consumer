namespace Consumer.Services;

public interface IMessageProcessor
{
    /// <summary>
    /// Process message payload. Return true if processed successfully and message must be deleted.
    /// Return false to indicate transient failure (message will become visible again).
    /// </summary>
    Task<bool> ProcessAsync(string messageText, CancellationToken cancellationToken);
}