using Azure.Storage.Queues;
using Consumer;
using Consumer.Configuration;
using Consumer.Services;
using Consumer.Workers;
using Microsoft.Extensions.Options;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddOptions<QueueSettings>()
    .BindConfiguration("Queue")
    .Validate(s => !string.IsNullOrWhiteSpace(s.ConnectionString) && !string.IsNullOrWhiteSpace(s.Name),
        "Queue connection and name are required.")
    .Validate(s => s.VisibilityTimeoutSeconds > 0, "VisibilityTimeoutSeconds must be positive.")
    .Validate(s => double.IsFinite(s.FixedConsumptionRatePerSecond) && s.FixedConsumptionRatePerSecond >= 0,
        "FixedConsumptionRatePerSecond must be finite and nonnegative.")
    .ValidateOnStart();

builder.Services.AddOptions<ProducerApiSettings>()
    .BindConfiguration("ProducerApi")
    .Validate(s => Uri.TryCreate(s.BaseUrl, UriKind.Absolute, out var uri) &&
        (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps),
        "ProducerApi:BaseUrl must be an HTTP(S) URL.")
    .ValidateOnStart();

builder.Services.AddSingleton<IQueueClient>(sp =>
{
    var settings = sp.GetRequiredService<IOptions<QueueSettings>>().Value;
    return new AzureQueueClientWrapper(new QueueClient(settings.ConnectionString, settings.Name));
});

builder.Services.AddKeyedSingleton<IQueueClient>("dead-letter", (sp, _) =>
{
    var settings = sp.GetRequiredService<IOptions<QueueSettings>>().Value;
    var name = string.IsNullOrWhiteSpace(settings.DeadLetterQueueName)
        ? settings.Name + "-dlq"
        : settings.DeadLetterQueueName;
    if (string.Equals(name, settings.Name, StringComparison.OrdinalIgnoreCase))
        throw new InvalidOperationException("The dead-letter queue must differ from the input queue.");

    return new AzureQueueClientWrapper(new QueueClient(settings.ConnectionString, name));
});

builder.Services.AddSingleton<IMessageProcessor, JsonMessageProcessor>();
builder.Services.AddTransient<HttpRetryHandler>();
builder.Services.AddHttpClient("ApiClient", (sp, client) =>
{
    var settings = sp.GetRequiredService<IOptions<ProducerApiSettings>>().Value;
    client.BaseAddress = new Uri(settings.BaseUrl);
    client.Timeout = TimeSpan.FromSeconds(8);
}).AddHttpMessageHandler<HttpRetryHandler>();

builder.Services.AddHostedService<QueueConsumerWorker>();
builder.Services.AddHostedService<HeartbeatWorker>();

await builder.Build().RunAsync();
