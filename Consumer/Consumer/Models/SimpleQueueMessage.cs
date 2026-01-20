namespace Consumer.Models
{
    public sealed class SimpleQueueMessage
    {
        public string MessageId { get; init; }
        public string PopReceipt { get; init; }
        public string MessageText { get; init; }
        public int DequeueCount { get; init; }

        public SimpleQueueMessage(string messageId, string popReceipt, string messageText, int dequeueCount)
        {
            MessageId = messageId;
            PopReceipt = popReceipt;
            MessageText = messageText;
            DequeueCount = dequeueCount;
        }
    }
}
