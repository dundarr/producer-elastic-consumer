using Consumer.Models;

namespace Consumer.Services;

public interface IQueueClient
{
    Task CreateIfNotExistsAsync(CancellationToken ct);
    Task<SimpleQueueMessage?> ReceiveMessageAsync(TimeSpan visibilityTimeout, CancellationToken ct);
    Task DeleteMessageAsync(string messageId, string popReceipt, CancellationToken ct);
    Task SendMessageAsync(string message, CancellationToken ct);
}
