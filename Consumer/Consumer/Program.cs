using Azure.Storage.Queues;
using Consumer;
using Consumer.Configuration;
using Consumer.Services;
using Consumer.Workers;
using Microsoft.Extensions.Options;

var builder = Host.CreateDefaultBuilder(args)
    .ConfigureAppConfiguration((context, config) =>
    {
        var env = context.HostingEnvironment;
        
        // Load base configuration
        config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
        
        // Load environment-specific configuration (e.g., appsettings.Docker.json)
        var envSpecificFile = $"appsettings.{env.EnvironmentName}.json";
        config.AddJsonFile(envSpecificFile, optional: true, reloadOnChange: true);
        
        // Environment variables take precedence
        config.AddEnvironmentVariables();
    })
    .ConfigureServices((context, services) =>
    {
        // Log which configuration files are being used
        var logger = LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger("Configuration");
        logger.LogInformation("Environment: {Environment}", context.HostingEnvironment.EnvironmentName);
        logger.LogInformation("Configuration file: appsettings.json");
        logger.LogInformation("Environment-specific file: appsettings.{Environment}.json", context.HostingEnvironment.EnvironmentName);
        
        var queueConnectionString = context.Configuration.GetSection("Queue")["ConnectionString"];
        logger.LogInformation("Queue ConnectionString (first 50 chars): {ConnectionString}", 
            queueConnectionString?.Length > 50 ? queueConnectionString[..50] + "..." : queueConnectionString);

        services.AddOptions<QueueSettings>()
            .Bind(context.Configuration.GetSection("Queue"))
            .Validate(s => s.VisibilityTimeoutSeconds > 0, "VisibilityTimeoutSeconds must be > 0");

        services.AddOptions<ProducerApiSettings>()
            .Bind(context.Configuration.GetSection("ProducerApi"))
            .Validate(s => !string.IsNullOrEmpty(s.BaseUrl));

        // Register QueueClient instances from configuration (testable)
        services.AddSingleton(sp =>
        {
            var opts = sp.GetRequiredService<IOptions<QueueSettings>>().Value;
            return new QueueClient(opts.ConnectionString, opts.Name);
        });

        // Optional: dead-letter queue client
        services.AddSingleton(sp =>
        {
            var opts = sp.GetRequiredService<IOptions<QueueSettings>>().Value;
            var dlqName = string.IsNullOrWhiteSpace(opts.DeadLetterQueueName) ? opts.Name + "-dlq" : opts.DeadLetterQueueName;
            return new QueueClient(opts.ConnectionString, dlqName);
        });

        // Register message processor (decoupled from worker)
        services.AddSingleton<IMessageProcessor, JsonMessageProcessor>();

        // Register an HttpMessageHandler that implements retry + jitter for outgoing HTTP requests
        services.AddTransient<HttpRetryHandler>();

        services.AddHostedService<QueueConsumerWorker>();
        services.AddHostedService<HeartbeatWorker>();

        // Typed HttpClient with our handler (retry logic moved to handler)
        services.AddHttpClient("ApiClient", client =>
        {
            var baseUrl = context.Configuration
                .GetSection("ProducerApi")
                .Get<ProducerApiSettings>()?
                .BaseUrl ?? string.Empty;

            client.BaseAddress = new Uri(baseUrl);
        })
        .AddHttpMessageHandler<HttpRetryHandler>();
    });

builder.Build().Run();
