namespace Consumer.UnitTests
{
    public class ReceiveRetryPolicyCancellationTests
    {
        [Fact]
        public async Task ExecuteAsync_CancellationDuringDelay_ThrowsOperationCanceledException()
        {
            // action that always fails
            Func<CancellationToken, Task<int>> action = ct =>
            {
                throw new InvalidOperationException("transient");
            };

            var policy = new ReceiveRetryPolicy(maxRetries: 5, baseDelay: TimeSpan.FromSeconds(1), maxJitterMs: 0, rng: new Random(1));

            using var cts = new CancellationTokenSource();

            var task = policy.ExecuteAsync(action, cts.Token);

            // Cancel immediately to ensure cancellation is observed during the retry delay
            cts.Cancel();

            await Assert.ThrowsAsync<OperationCanceledException>(() => task);
        }
    }
}
