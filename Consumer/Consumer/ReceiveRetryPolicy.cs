namespace Consumer
{
    // testable retry helper implementing exponential backoff + jitter.
    public sealed class ReceiveRetryPolicy
    {
        private readonly int _maxRetries;
        private readonly TimeSpan _baseDelay;
        private readonly int _maxJitterMs;
        private readonly Random _rng;

        public ReceiveRetryPolicy(int maxRetries, TimeSpan baseDelay, int maxJitterMs = 500, Random? rng = null)
        {
            _maxRetries = maxRetries;
            _baseDelay = baseDelay;
            _maxJitterMs = maxJitterMs;
            _rng = rng ?? new Random();
        }

        // Execute an arbitrary async operation with retry semantics.
        public async Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken ct)
        {
            int attempt = 0;

            while (true)
            {
                try
                {
                    return await action(ct);
                }
                catch (TaskCanceledException)
                {
                    // Task.Delay uses TaskCanceledException when the token is cancelled during the delay.
                    // Convert to OperationCanceledException (exact type) so callers that require the
                    // exact exception type (as in unit tests) observe it.
                    throw new OperationCanceledException(ct);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    // Preserve cancellation so callers can shut down promptly. 
                    throw;
                }
                catch (Exception)
                {
                    attempt++;
                    if (attempt > _maxRetries)
                        throw;

                    var expo = Math.Pow(2, attempt);
                    var jitterMs = _rng.Next(0, _maxJitterMs + 1);
                    var delay = TimeSpan.FromMilliseconds(expo * _baseDelay.TotalMilliseconds)
                                + TimeSpan.FromMilliseconds(jitterMs);

                    try
                    {
                        await Task.Delay(delay, ct);
                    }
                    catch (TaskCanceledException)
                    {
                        throw new OperationCanceledException(ct);
                    }
                }
            }
        }
    }
}