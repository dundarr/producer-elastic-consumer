namespace Consumer
{
    // Simple delegating handler implementing exponential backoff + jitter for HTTP requests.
    // Keep small and explicit so it's easy to replace later with Microsoft.Extensions.Http.Resilience.
    public class HttpRetryHandler : DelegatingHandler
    {
        private readonly ILogger<HttpRetryHandler> _logger;
        private readonly int _maxRetries = 3;
        private readonly TimeSpan _baseDelay = TimeSpan.FromSeconds(1);
        private readonly Random _jitterer = new();

        public HttpRetryHandler(ILogger<HttpRetryHandler> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            int attempt = 0;

            while (true)
            {
                try
                {
                    var response = await base.SendAsync(request, cancellationToken);
                    if (response.IsSuccessStatusCode)
                        return response;

                    // treat non-success as transient for retries
                    attempt++;
                    if (attempt > _maxRetries)
                        return response;

                    var delay = GetDelay(attempt);
                    _logger.LogWarning("HTTP request failed with status {Status}. Retrying {Attempt}/{Max} after {Delay}.",
                        response.StatusCode, attempt, _maxRetries, delay);

                    await Task.Delay(delay, cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    attempt++;
                    if (attempt > _maxRetries)
                        throw;

                    var delay = GetDelay(attempt);
                    _logger.LogWarning(ex, "HTTP request exception. Retrying {Attempt}/{Max} after {Delay}.", attempt, _maxRetries, delay);
                    await Task.Delay(delay, cancellationToken);
                }
            }
        }

        private TimeSpan GetDelay(int attempt)
        {
            // exponential backoff * base + jitter (ms)
            var expo = Math.Pow(2, attempt);
            var jitterMs = _jitterer.Next(0, 300);
            return TimeSpan.FromMilliseconds(expo * _baseDelay.TotalMilliseconds) + TimeSpan.FromMilliseconds(jitterMs);
        }
    }
}