using System.Text.Json;

namespace Consumer.Services;

public class JsonMessageProcessor : IMessageProcessor
{
    private readonly ILogger<JsonMessageProcessor> _logger;

    public JsonMessageProcessor(ILogger<JsonMessageProcessor> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task<bool> ProcessAsync(string messageText, CancellationToken cancellationToken)
    {
        try
        {
            // deserialize into a concrete type or handle generically
            var obj = JsonSerializer.Deserialize<object>(messageText);
            _logger.LogInformation("Handled message payload of type {Type}", obj?.GetType()?.Name ?? "null");
            // business logic here...
            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Message processing failed");
            // decide whether this is transient or permanent
            return Task.FromResult(false);
        }
    }
}