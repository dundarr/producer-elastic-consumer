using System.Net.Http.Json;

namespace Consumer.Workers;

public class HeartbeatWorker(IHttpClientFactory factory, ILogger<HeartbeatWorker> logger) : BackgroundService
{
    private readonly Guid _consumerId = Guid.NewGuid();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(10));
        try
        {
            do
            {
                try
                {
                    await NotifyAsync("register", stoppingToken);
                }
                catch (HttpRequestException ex)
                {
                    logger.LogWarning(ex, "Could not register consumer {ConsumerId}", _consumerId);
                }
                catch (OperationCanceledException ex) when (!stoppingToken.IsCancellationRequested)
                {
                    logger.LogWarning(ex, "Registration timed out for consumer {ConsumerId}", _consumerId);
                }
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));

        try
        {
            await NotifyAsync("unregister", timeout.Token);
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException)
        {
            logger.LogWarning(ex, "Could not unregister consumer {ConsumerId}", _consumerId);
        }
    }

    private async Task NotifyAsync(string action, CancellationToken ct)
    {
        using var client = factory.CreateClient("ApiClient");
        using var response = await client.PostAsJsonAsync($"/producer/consumer/{action}", _consumerId, ct);
        response.EnsureSuccessStatusCode();
    }
}
