using System.Net;

namespace Consumer;

public class HttpRetryHandler(ILogger<HttpRetryHandler> logger) : DelegatingHandler
{
    private const int MaxRetries = 3;

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        for (var retry = 0; ; retry++)
        {
            try
            {
                var response = await base.SendAsync(request, ct);
                var transient = response.StatusCode is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests
                    || (int)response.StatusCode >= 500;
                if (!transient || retry == MaxRetries)
                    return response;

                logger.LogDebug("Retrying heartbeat after HTTP {Status}", response.StatusCode);
                response.Dispose();
            }
            catch (HttpRequestException ex) when (retry < MaxRetries)
            {
                logger.LogDebug(ex, "Retrying heartbeat after a connection error");
            }

            await Task.Delay(TimeSpan.FromMilliseconds(500 * (1 << retry) + Random.Shared.Next(300)), ct);
        }
    }
}
