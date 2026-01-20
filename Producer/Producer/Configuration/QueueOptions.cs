namespace Producer.Configuration;

public class QueueOptions
{
    public string ConnectionString { get; set; }
    public string QueueName { get; set; }

    public int MessagesPerSecond { get; set; } = 1;
}