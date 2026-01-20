namespace Producer.Dto;

/// <summary>
/// Data transfer object for message production rate.
/// </summary>
public class RateDto
{
    /// <summary>
    /// Number of messages to produce per second.
    /// </summary>
    /// <example>50</example>
    public int Rate { get; set; }
}
