using Azure.Storage.Queues;
using Consumer.Models;

namespace Consumer.Services;

public class AzureQueueClientWrapper(QueueClient client) : IQueueClient
{
    public Task CreateIfNotExistsAsync(CancellationToken ct) =>
        client.CreateIfNotExistsAsync(cancellationToken: ct);

    public async Task<SimpleQueueMessage?> ReceiveMessageAsync(TimeSpan visibilityTimeout, CancellationToken ct)
    {
        var response = await client.ReceiveMessageAsync(visibilityTimeout, ct);
        var message = response?.Value;

        return message is null
            ? null
            : new SimpleQueueMessage(message.MessageId, message.PopReceipt, message.MessageText, (int)message.DequeueCount);
    }

    public Task DeleteMessageAsync(string messageId, string popReceipt, CancellationToken ct) =>
        client.DeleteMessageAsync(messageId, popReceipt, ct);

    public Task SendMessageAsync(string message, CancellationToken ct) =>
        client.SendMessageAsync(message, cancellationToken: ct);
}
