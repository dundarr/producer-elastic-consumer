namespace Producer.Dto;

/// <summary>
/// Data transfer object for producer status.
/// </summary>
public class StatusDto
{
    /// <summary>
    /// Current status of the producer service.
    /// </summary>
    /// <example>started</example>
    public string Status { get; set; }
}
