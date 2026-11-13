namespace Dashboard;

public record TelemetrySnapshot(
    int QueueLength,
    int ConsumerCount,
    bool IsRunning,
    int Rate,
    DateTimeOffset SampledAt);
