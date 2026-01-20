using Azure.Storage.Queues;
using Consumer.Models;

namespace Consumer.Services
{
    public class AzureQueueClientWrapper : IQueueClient
    {
        private readonly QueueClient _client;

        public AzureQueueClientWrapper(QueueClient client)
        {
            _client = client;
        }

        public void CreateIfNotExists() => _client.CreateIfNotExists();

        public async Task<SimpleQueueMessage?> ReceiveMessageAsync(TimeSpan visibilityTimeout, CancellationToken ct)
        {
            var response = await _client.ReceiveMessageAsync(visibilityTimeout, ct);
            if (response?.Value == null)
                return null;

            var m = response.Value;
            return new SimpleQueueMessage(m.MessageId, m.PopReceipt, m.MessageText, (int)m.DequeueCount);
        }

        public Task DeleteMessageAsync(string messageId, string popReceipt, CancellationToken ct) => _client.DeleteMessageAsync(messageId, popReceipt, ct);
    }
}
