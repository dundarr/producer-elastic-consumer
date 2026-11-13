namespace Dashboard;

public class ProducerService(HttpClient http)
{
    public async Task<TelemetrySnapshot> GetSnapshotAsync(CancellationToken ct) =>
        await http.GetFromJsonAsync<TelemetrySnapshot>("/metrics/snapshot", ct)
        ?? throw new HttpRequestException("The producer returned no telemetry.");

    public Task SetRunningAsync(bool running, CancellationToken ct) =>
        PostAsync(running ? "/producer/start" : "/producer/stop", ct);

    public Task SetRateAsync(int rate, CancellationToken ct) =>
        PostAsync($"/producer/rate/{rate}", ct);

    private async Task PostAsync(string path, CancellationToken ct)
    {
        using var response = await http.PostAsync(path, null, ct);
        response.EnsureSuccessStatusCode();
    }
}
