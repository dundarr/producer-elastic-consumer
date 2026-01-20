using Azure.Core;
using Azure.Storage.Queues;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using Producer.Configuration;
using Producer.Services;
using Producer.Workers;
using System;

var builder = WebApplication.CreateBuilder(args);

// Configure environment-specific settings
var environment = builder.Environment.EnvironmentName;
builder.Configuration
    .SetBasePath(builder.Environment.ContentRootPath)
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"appsettings.{environment}.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables(); // Override with environment variables (e.g., Queue__ConnectionString)

// Log startup information
var startupLogger = LoggerFactory.Create(logging => logging.AddConsole()).CreateLogger("Startup");
startupLogger.LogInformation("Starting Producer application in {Environment} environment", environment);

// Bind Kestrel to port 3000 on all network interfaces
builder.WebHost.UseUrls("http://0.0.0.0:3000");

// Configure logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

// Bind Queue configuration section to strongly-typed options
builder.Services.Configure<QueueOptions>(builder.Configuration.GetSection("Queue"));

// Register QueueClient as singleton with retry configuration
builder.Services.AddSingleton(sp =>
{
    var opts = sp.GetRequiredService<IOptions<QueueOptions>>().Value;
    var logger = sp.GetRequiredService<ILogger<Program>>();
    
    // Validate required configuration
    if (string.IsNullOrWhiteSpace(opts.ConnectionString))
    {
        logger.LogError("Queue:ConnectionString is not configured. Check environment variables or appsettings");
        throw new InvalidOperationException("Queue:ConnectionString is not configured.");
    }

    if (string.IsNullOrWhiteSpace(opts.QueueName))
    {
        logger.LogError("Queue:QueueName is not configured");
        throw new InvalidOperationException("Queue:QueueName is not configured.");
    }

    logger.LogInformation("Configuring QueueClient for queue: {QueueName}", opts.QueueName);

    // Configure retry policy: exponential backoff with max 5 retries
    var clientOptions = new QueueClientOptions
    {
        Retry =
        {
            MaxRetries = 5,
            Delay = TimeSpan.FromSeconds(1),
            Mode = RetryMode.Exponential
        }
    };

    return new QueueClient(opts.ConnectionString, opts.QueueName, clientOptions);
});

// Register resilience pipeline for queue send operations
builder.Services.AddSingleton<IQueueSendPolicy, QueueSendPolicy>();

// Register producer control as singleton (shared state between controller and worker)
builder.Services.AddSingleton<ProducerWorkerControl>();
builder.Services.AddSingleton<IProducerWorkerControl>(sp => sp.GetRequiredService<ProducerWorkerControl>());

// Register background workers
builder.Services.AddSingleton<ConsumerCleanupWorker>();
builder.Services.AddSingleton<ProducerWorker>();

// Register hosted services (BackgroundService instances)
builder.Services.AddHostedService(sp => sp.GetRequiredService<ProducerWorker>());
builder.Services.AddHostedService(sp => sp.GetRequiredService<ConsumerCleanupWorker>());

// Register consumer registry for tracking active consumers
builder.Services.AddSingleton<IConsumerRegistry, ConsumerRegistry>();

// Register queue metrics service
builder.Services.AddSingleton<IQueueMetricsService, QueueMetricsService>();

// Register ASP.NET Core services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Producer API", Version = "v1" });
});

// Configure CORS: Allow all origins, headers, and methods
// WARNING: This is permissive for development - restrict in production
builder.Services.AddCors(options =>
{
    options.AddPolicy("OpenCorsPolicy", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

// Log configuration values (excluding sensitive connection strings)
var config = app.Services.GetRequiredService<IOptions<QueueOptions>>().Value;
app.Logger.LogInformation("Queue configured: Name={QueueName}, MessagesPerSecond={MessagesPerSecond}", 
    config.QueueName, config.MessagesPerSecond);

// Enable Swagger in all environments (remove in production if not needed)
app.UseSwagger();
app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Producer API v1"));

// Apply CORS policy
app.UseCors("OpenCorsPolicy");

// Map controller endpoints
app.MapControllers();

// Start the application
app.Run();
