using Consumer.Models;

namespace Consumer.Services
{
    public interface IQueueClient
    {
        void CreateIfNotExists();
        Task<SimpleQueueMessage?> ReceiveMessageAsync(TimeSpan visibilityTimeout, CancellationToken ct);
        Task DeleteMessageAsync(string messageId, string popReceipt, CancellationToken ct);
    }
}
