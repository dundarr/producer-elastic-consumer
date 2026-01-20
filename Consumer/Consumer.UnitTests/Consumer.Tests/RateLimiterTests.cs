using Consumer.Configuration;
using Consumer.Services;
using Consumer.Workers;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Consumer.UnitTests
{
    public class RateLimiterTests
    {
        private QueueConsumerWorker CreateWorker(double rate)
        {
            var settings = new QueueSettings
            {
                ConnectionString = "UseDevelopmentStorage=true",
                Name = "test",
                FixedConsumptionRatePerSecond = rate,
                VisibilityTimeoutSeconds = 30
            };

            var options = Options.Create(settings);
            var logger = NullLogger<QueueConsumerWorker>.Instance;

            // simple stub IMessageProcessor
            IMessageProcessor processor = new StubProcessor();

            return new QueueConsumerWorker(logger, options, processor);
        }

        [Fact]
        public void CalculateWaitForNextStart_FirstCallAllowsImmediate_SecondCallRequiresWait()
        {
            var worker = CreateWorker(rate: 2.0); // 2 messages/sec -> 0.5s between starts
            var now = DateTimeOffset.UtcNow;

            var wait1 = worker.CalculateWaitForNextStart(now);
            Assert.Equal(TimeSpan.Zero, wait1);

            // immediate second call - should require waiting (close to 500ms)
            var wait2 = worker.CalculateWaitForNextStart(now);
            Assert.True(wait2 > TimeSpan.Zero);
        }

        [Fact]
        public void CalculateWaitForNextStart_AfterSlotPassed_AllowsImmediate()
        {
            var worker = CreateWorker(rate: 2.0);
            var now = DateTimeOffset.UtcNow;

            var wait1 = worker.CalculateWaitForNextStart(now);
            Assert.Equal(TimeSpan.Zero, wait1);

            // simulate time after the next slot
            var later = now.AddMilliseconds(600); // > 500ms
            var wait2 = worker.CalculateWaitForNextStart(later);
            Assert.Equal(TimeSpan.Zero, wait2);
        }

        private class StubProcessor : IMessageProcessor
        {
            public Task<bool> ProcessAsync(string message, CancellationToken ct) => Task.FromResult(true);
        }
    }
}