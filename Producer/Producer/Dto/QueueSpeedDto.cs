using System;

namespace Producer.Dto
{
    /// <summary>
    /// Data transfer object for queue speed response.
    /// </summary>
    public class QueueSpeedDto
    {
        /// <summary>
        /// Queue growth/consumption rate in messages per second.
        /// Positive values indicate queue growth (production > consumption).
        /// Negative values indicate consumption (consumption > production).
        /// Zero indicates balance (production = consumption).
        /// </summary>
        /// <example>3.67</example>
        public double QueueSpeed { get; set; }
        
        /// <summary>
        /// Timestamp when the measurement was completed (UTC).
        /// </summary>
        /// <example>2024-12-18T14:30:03Z</example>
        public DateTime Timestamp { get; set; }
    }
}
