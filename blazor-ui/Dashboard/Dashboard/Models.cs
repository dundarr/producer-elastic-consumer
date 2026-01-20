namespace Dashboard
{
    public record StatusDto(string? Status);
    public record RateDto(int Rate);
    public record QueueLengthDto(long QueueLength, DateTime Timestamp);
    public record QueueSpeedDto(double QueueSpeed, DateTime Timestamp);
}
