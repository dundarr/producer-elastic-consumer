using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Producer.Services;
using System;
using System.Threading.Tasks;

namespace Producer.Controllers;

[ApiController]
[Route("metrics/snapshot")]
public class TelemetryController(
    IQueueMetricsService queue,
    IProducerWorkerControl producer,
    IConsumerRegistry consumers,
    ILogger<TelemetryController> logger) : ControllerBase
{
    [HttpGet("/metrics/production-rate")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public ProductionRateMetric GetProductionRate() =>
        new(producer.IsRunning() ? producer.GetRate() : 0);

    [HttpGet]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<ActionResult<TelemetrySnapshot>> Get()
    {
        try
        {
            var length = await queue.GetQueueLengthAsync();
            var sampledAt = DateTimeOffset.UtcNow;
            var registered = await consumers.GetRegisteredAsync();
            return new TelemetrySnapshot(length, registered.Count, producer.IsRunning(), producer.GetRate(), sampledAt);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not sample queue telemetry");
            return Problem(statusCode: 503, title: "Queue telemetry is unavailable");
        }
    }
}

public record TelemetrySnapshot(int QueueLength, int ConsumerCount, bool IsRunning, int Rate, DateTimeOffset SampledAt);

public record ProductionRateMetric(int MessagesPerSecond);
