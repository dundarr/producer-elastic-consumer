using Microsoft.AspNetCore.Mvc;
using Producer.Services;
using System;
using System.Threading.Tasks;

[ApiController]
[Route("producer/consumer")]
public class ConsumersController : ControllerBase
{
    private readonly IConsumerRegistry _registry;

    public ConsumersController(IConsumerRegistry registry)
    {
        _registry = registry;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] Guid consumerId)
    {
        await _registry.RegisterAsync(consumerId);
        return Ok(new { registered = consumerId });
    }

    [HttpPost("unregister")]
    public async Task<IActionResult> Unregister([FromBody] Guid consumerId)
    {
        await _registry.UnregisterAsync(consumerId);
        return Ok(new { unregistered = consumerId });
    }

    // GET producer/consumer
    [HttpGet]
    public async Task<IActionResult> List() => Ok(await _registry.GetRegisteredAsync());

    // GET producer/consumer/count
    [HttpGet("count")]
    public async Task<IActionResult> Count()
    {
        return Ok((await _registry.GetRegisteredAsync()).Count);
    }
}
