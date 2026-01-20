using Consumer.Configuration;
using Consumer.Models;
using Consumer.Services;
using Consumer.Workers;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Consumer.UnitTests
{
    public class QueueConsumerWorkerProcessMessageTests
    {
        private QueueConsumerWorker CreateWorker(IQueueClient queueClient, IMessageProcessor processor, int deadLetterThreshold = 5)
        {
            var settings = new QueueSettings
            {
                ConnectionString = "UseDevelopmentStorage=true",
                Name = "test",
                FixedConsumptionRatePerSecond = 0,
                VisibilityTimeoutSeconds = 30,
                DeadLetterThreshold = deadLetterThreshold
            };

            var options = Options.Create(settings);
            var logger = NullLogger<QueueConsumerWorker>.Instance;

            return new QueueConsumerWorker(logger, options, processor, queueClient);
        }

        private class StubProcessor : IMessageProcessor
        {
            private readonly bool _result;

            public StubProcessor(bool result)
            {
                _result = result;
            }

            public Task<bool> ProcessAsync(string message, CancellationToken ct) => Task.FromResult(_result);
        }

        private class FakeQueueClient : IQueueClient
        {
            public string? DeletedMessageId;
            public string? DeletedPopReceipt;
            public string? SentMessage;

            public void CreateIfNotExists() { }

            public Task<SimpleQueueMessage?> ReceiveMessageAsync(TimeSpan visibilityTimeout, CancellationToken ct) => Task.FromResult<SimpleQueueMessage?>(null);

            public Task DeleteMessageAsync(string messageId, string popReceipt, CancellationToken ct)
            {
                DeletedMessageId = messageId;
                DeletedPopReceipt = popReceipt;
                return Task.CompletedTask;
            }

            public Task SendMessageAsync(string message, CancellationToken ct)
            {
                SentMessage = message;
                return Task.CompletedTask;
            }
        }

        [Fact]
        public async Task ProcessMessage_Success_DeletesMessage()
        {
            var fake = new FakeQueueClient();
            var processor = new StubProcessor(true);
            var worker = CreateWorker(fake, processor);

            var message = new SimpleQueueMessage("id1", "pr1", "hello", 1);

            await worker.ProcessMessage(message, CancellationToken.None);

            Assert.Equal("id1", fake.DeletedMessageId);
            Assert.Equal("pr1", fake.DeletedPopReceipt);
        }

        [Fact]
        public async Task ProcessMessage_FailureBelowThreshold_DoesNotDelete()
        {
            var fake = new FakeQueueClient();
            var processor = new StubProcessor(false);
            var worker = CreateWorker(fake, processor, deadLetterThreshold: 5);

            var message = new SimpleQueueMessage("id2", "pr2", "bad", 1);

            await worker.ProcessMessage(message, CancellationToken.None);

            Assert.Null(fake.DeletedMessageId);
        }

        [Fact]
        public async Task ProcessMessage_FailureAtOrAboveThreshold_DeletesAndWouldMoveToDlq()
        {
            var fake = new FakeQueueClient();
            var processor = new StubProcessor(false);
            var worker = CreateWorker(fake, processor, deadLetterThreshold: 2);

            var message = new SimpleQueueMessage("id3", "pr3", "bad", 2);

            await worker.ProcessMessage(message, CancellationToken.None);

            Assert.Equal("id3", fake.DeletedMessageId);
        }
    }
}
