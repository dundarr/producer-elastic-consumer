namespace Consumer.UnitTests
{
    public class ReceiveRetryPolicyEdgeCasesTests
    {
        [Fact]
        public async Task ExecuteAsync_PropagatesOperationCanceledWhenTokenAlreadyCancelled()
        {
            var attempts = 0;

            Func<CancellationToken, Task<int>> action = ct =>
            {
                attempts++;
                throw new OperationCanceledException();
            };

            var policy = new ReceiveRetryPolicy(maxRetries: 5, baseDelay: TimeSpan.FromMilliseconds(1), maxJitterMs: 0, rng: new Random(1));

            using var cts = new CancellationTokenSource();
            cts.Cancel(); // token already cancelled

            await Assert.ThrowsAsync<OperationCanceledException>(() => policy.ExecuteAsync(action, cts.Token));

            Assert.Equal(1, attempts);
        }

        [Fact]
        public async Task ExecuteAsync_TreatsOperationCanceledExceptionAsTransientWhenTokenNotCancelled()
        {
            var attempts = 0;

            Func<CancellationToken, Task<int>> action = ct =>
            {
                attempts++;
                throw new OperationCanceledException();
            };

            var policy = new ReceiveRetryPolicy(maxRetries: 2, baseDelay: TimeSpan.FromMilliseconds(1), maxJitterMs: 0, rng: new Random(1));

            await Assert.ThrowsAsync<OperationCanceledException>(() => policy.ExecuteAsync(action, CancellationToken.None));

            // initial attempt + 2 retries
            Assert.Equal(3, attempts);
        }
    }
}
