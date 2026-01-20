namespace Consumer.UnitTests
{
    public class ReceiveRetryPolicyTests
    {
        [Fact]
        public async Task ExecuteAsync_RetriesUntilSuccess()
        {
            var attempts = 0;

            // action that fails twice then succeeds
            Func<CancellationToken, Task<int>> action = ct =>
            {
                attempts++;
                if (attempts < 3)
                    throw new InvalidOperationException("transient");
                return Task.FromResult(42);
            };

            var policy = new ReceiveRetryPolicy(maxRetries: 5, baseDelay: TimeSpan.FromMilliseconds(1), maxJitterMs: 0, rng: new Random(1));

            var result = await policy.ExecuteAsync(action, CancellationToken.None);

            Assert.Equal(42, result);
            Assert.Equal(3, attempts);
        }

        [Fact]
        public async Task ExecuteAsync_ThrowsAfterMaxRetries()
        {
            var attempts = 0;

            Func<CancellationToken, Task<int>> action = ct =>
            {
                attempts++;
                throw new InvalidOperationException("permanent");
            };

            var policy = new ReceiveRetryPolicy(maxRetries: 2, baseDelay: TimeSpan.FromMilliseconds(1), maxJitterMs: 0, rng: new Random(1));

            await Assert.ThrowsAsync<InvalidOperationException>(() => policy.ExecuteAsync(action, CancellationToken.None));
            Assert.Equal(3, attempts); // initial attempt + 2 retries = 3 attempts
        }
    }
}