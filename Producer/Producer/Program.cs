using Azure.Core;
using Azure.Storage.Queues;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using Producer.Configuration;
using Producer.Services;
using Producer.Workers;
using System;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddOptions<QueueOptions>()
    .BindConfiguration("Queue")
    .Validate(o => !string.IsNullOrWhiteSpace(o.ConnectionString) && !string.IsNullOrWhiteSpace(o.QueueName),
        "Queue connection and name are required.")
    .ValidateOnStart();

builder.Services.AddSingleton(sp =>
{
    var options = sp.GetRequiredService<IOptions<QueueOptions>>().Value;
    return new QueueClient(options.ConnectionString, options.QueueName, new QueueClientOptions
    {
        Retry =
        {
            MaxRetries = 2,
            Delay = TimeSpan.FromMilliseconds(250),
            Mode = RetryMode.Exponential,
            NetworkTimeout = TimeSpan.FromSeconds(2)
        }
    });
});

builder.Services.AddSingleton<IQueueSendPolicy, QueueSendPolicy>();
builder.Services.AddSingleton<IProducerWorkerControl, ProducerWorkerControl>();
builder.Services.AddSingleton<IConsumerRegistry, ConsumerRegistry>();
builder.Services.AddSingleton<IQueueMetricsService>(sp => new QueueMetricsService(
    sp.GetRequiredService<QueueClient>(), sp.GetRequiredService<ILogger<QueueMetricsService>>()));
builder.Services.AddHostedService<ProducerWorker>();
builder.Services.AddHostedService<ConsumerCleanupWorker>();

builder.Services.AddControllers();
builder.Services.AddSwaggerGen(options =>
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "Producer API", Version = "v1" }));
builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
    policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();
app.UseSwagger();
app.UseSwaggerUI();
app.UseCors();
app.MapControllers();
app.Run();
