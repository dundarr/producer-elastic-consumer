namespace Consumer.Configuration;

public class QueueSettings
{
    public string ConnectionString { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;

    public string DeadLetterQueueName { get; set; } = string.Empty;

    public int DeadLetterThreshold { get; set; } = 5;

    public int VisibilityTimeoutSeconds { get; set; }
    public double FixedConsumptionRatePerSecond { get; set; }

    public int ReceiveMaxRetries { get; set; } = 5;
    public int ReceiveBaseDelayMs { get; set; } = 200;
}