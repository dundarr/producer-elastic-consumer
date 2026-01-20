using Microsoft.AspNetCore.Mvc;
using Producer.Dto;
using Producer.Services;
using System;
using System.Threading.Tasks;

[ApiController]
[Route("producer")]
public class QueueMetricsController : ControllerBase
{
    private readonly IQueueMetricsService _metricsService;

    public QueueMetricsController(IQueueMetricsService metricsService)
    {
        _metricsService = metricsService;
    }

    [HttpGet("length")]
    public async Task<IActionResult> GetQueueLength()
    {
        var length = await _metricsService.GetQueueLengthAsync();

        return Ok(new QueueLengthDto
        {
            QueueLength = length,
            Timestamp = DateTime.UtcNow
        });
    }

    [HttpGet("speed")]
    public async Task<IActionResult> GetQueueSpeed()
    {
        double speed = await _metricsService.GetQueueSpeedAsync();

        return Ok(new QueueSpeedDto
        {
            QueueSpeed = speed,
            Timestamp = DateTime.UtcNow
        });
    }

}
