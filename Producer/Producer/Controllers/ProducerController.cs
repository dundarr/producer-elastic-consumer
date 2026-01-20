using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Producer.Dto;
using Producer.Services;

namespace Producer.Controllers;

/// <summary>
/// Controller for managing message production operations.
/// Provides endpoints to start, stop, and monitor the producer service.
/// </summary>
[ApiController]
[Route("producer")]
[Produces("application/json")]
public class ProducerController : ControllerBase
{
    private readonly IProducerWorkerControl _producer;

    public ProducerController(IProducerWorkerControl producer)
    {
        _producer = producer;
    }

    /// <summary>
    /// Starts message production.
    /// </summary>
    /// <remarks>
    /// Initiates the background worker to start sending messages to the queue.
    /// The producer will send messages at the configured rate (default: 1 message/second).
    /// 
    /// Sample request:
    /// 
    ///     POST /producer/start
    /// 
    /// </remarks>
    /// <response code="200">Producer started successfully</response>
    /// <returns>Status indicating the producer has started</returns>
    [HttpPost("start")]
    [ProducesResponseType(typeof(StatusDto), StatusCodes.Status200OK)]
    public ActionResult<StatusDto> Start()
    {
        _producer.StartProducing();
        return Ok(new StatusDto { Status = "started" });
    }

    /// <summary>
    /// Stops message production (pauses the producer).
    /// </summary>
    /// <remarks>
    /// Pauses the background worker, stopping message production.
    /// The queue remains intact and can be resumed with the start endpoint.
    /// 
    /// Sample request:
    /// 
    ///     POST /producer/stop
    /// 
    /// </remarks>
    /// <response code="200">Producer stopped successfully</response>
    /// <returns>Status indicating the producer has stopped</returns>
    [HttpPost("stop")]
    [ProducesResponseType(typeof(StatusDto), StatusCodes.Status200OK)]
    public ActionResult<StatusDto> Stop()
    {
        _producer.StopProducing();
        return Ok(new StatusDto { Status = "stopped" });
    }

    /// <summary>
    /// Checks if the producer is currently running.
    /// </summary>
    /// <remarks>
    /// Returns the current state of the producer service.
    /// 
    /// Sample request:
    /// 
    ///     GET /producer/is-running
    /// 
    /// </remarks>
    /// <response code="200">Returns true if producer is running, false if stopped</response>
    /// <returns>Boolean indicating whether the producer is active</returns>
    [HttpGet("is-running")]
    [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
    public ActionResult<bool> IsRunning()
    {
        return Ok(_producer.IsRunning());
    }

    /// <summary>
    /// Gets the current message production rate (messages per second).
    /// </summary>
    /// <remarks>
    /// Returns the current configured rate at which messages are being produced.
    /// 
    /// Sample request:
    /// 
    ///     GET /producer/rate
    /// 
    /// </remarks>
    /// <response code="200">Returns the current production rate</response>
    /// <returns>Current messages per second rate</returns>
    [HttpGet("rate")]
    [ProducesResponseType(typeof(RateDto), StatusCodes.Status200OK)]
    public ActionResult<RateDto> Rate()
    {
        return Ok(new RateDto { Rate = _producer.GetRate() });
    }

    /// <summary>
    /// Updates the message production rate at runtime.
    /// </summary>
    /// <remarks>
    /// Changes the rate at which messages are produced without restarting the service.
    /// The rate must be a positive integer (minimum value is 1).
    /// Values less than 1 will be automatically adjusted to 1.
    /// 
    /// Sample request:
    /// 
    ///     POST /producer/rate/50
    /// 
    /// </remarks>
    /// <param name="messagesPerSecond">New rate in messages per second (minimum: 1)</param>
    /// <response code="200">Rate updated successfully</response>
    /// <returns>Confirmation with the new rate</returns>
    [HttpPost("rate/{messagesPerSecond:int}")]
    [ProducesResponseType(typeof(RateDto), StatusCodes.Status200OK)]
    public ActionResult<RateDto> UpdateRate([FromRoute] int messagesPerSecond)
    {
        _producer.SetRate(messagesPerSecond);
        return Ok(new RateDto { Rate = _producer.GetRate() });
    }
}
