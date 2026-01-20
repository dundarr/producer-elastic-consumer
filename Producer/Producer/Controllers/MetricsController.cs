using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Producer.Dto;
using Producer.Services;
using System;
using System.Threading.Tasks;

namespace Producer.Controllers;

/// <summary>
/// Controller for retrieving queue metrics and statistics.
/// Provides endpoints to monitor queue length and consumption/production speed.
/// </summary>
[ApiController]
[Route("metrics")]
[Produces("application/json")]
public class MetricsController : ControllerBase
{
    private readonly IQueueMetricsService _metricsService;

    public MetricsController(IQueueMetricsService metricsService)
    {
        _metricsService = metricsService;
    }

    /// <summary>
    /// Gets the current number of messages in the queue.
    /// </summary>
    /// <remarks>
    /// Returns the approximate number of messages currently waiting in the Azure Queue Storage.
    /// Note: Azure Queue Storage returns an approximate count, not an exact count.
    /// If the queue doesn't exist, it will be created automatically and return 0.
    /// 
    /// Sample request:
    /// 
    ///     GET /metrics/length
    /// 
    /// </remarks>
    /// <response code="200">Returns the current queue length with timestamp</response>
    /// <response code="500">If there's an error reading queue properties</response>
    /// <returns>Number of messages in the queue with timestamp</returns>
    [HttpGet("length")]
    [ProducesResponseType(typeof(QueueLengthDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<QueueLengthDto>> GetQueueLength()
    {
        try
        {
            var length = await _metricsService.GetQueueLengthAsync();
            return new QueueLengthDto 
            { 
                QueueLength = length,
                Timestamp = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, 
                new { error = "Failed to retrieve queue length", details = ex.Message });
        }
    }

    /// <summary>
    /// Calculates the queue growth/consumption rate.
    /// </summary>
    /// <remarks>
    /// Measures the queue length at two points in time (3 seconds apart) and calculates the rate of change.
    /// 
    /// **Important**: This endpoint takes ~3 seconds to complete as it measures the rate over time.
    /// 
    /// Return values:
    /// - **Positive value**: Queue is growing (production rate > consumption rate)
    /// - **Negative value**: Queue is shrinking (consumption rate > production rate)  
    /// - **Zero**: Balanced (production rate = consumption rate)
    /// 
    /// If the queue doesn't exist, it will be created automatically.
    /// 
    /// Sample request:
    /// 
    ///     GET /metrics/speed
    /// 
    /// Example response when queue is growing:
    /// 
    ///     {
    ///       "speed": 3.67,
    ///       "timestamp": "2024-12-18T14:30:00Z"
    ///     }
    /// 
    /// This means the queue is growing at ~3.67 messages per second.
    /// 
    /// </remarks>
    /// <response code="200">Returns the calculated queue speed (messages per second) with timestamp</response>
    /// <response code="500">If there's an error calculating queue speed</response>
    /// <returns>Queue growth/consumption rate in messages per second with timestamp</returns>
    [HttpGet("speed")]
    [ProducesResponseType(typeof(QueueSpeedDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<QueueSpeedDto>> GetQueueSpeed()
    {
        try
        {
            var speed = await _metricsService.GetQueueSpeedAsync();
            return new QueueSpeedDto 
            { 
                QueueSpeed = speed,
                Timestamp = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, 
                new { error = "Failed to calculate queue speed", details = ex.Message });
        }
    }
}
