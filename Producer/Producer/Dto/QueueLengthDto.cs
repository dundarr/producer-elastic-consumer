using System;

namespace Producer.Dto
{
    /// <summary>
    /// Data transfer object for queue length response.
    /// </summary>
    public class QueueLengthDto
    {
        /// <summary>
        /// Approximate number of messages currently in the queue.
        /// Note: Azure Queue Storage returns an approximate count, not an exact count.
        /// </summary>
        /// <example>42</example>
        public long QueueLength { get; set; }
        
        /// <summary>
        /// Timestamp when the measurement was taken (UTC).
        /// </summary>
        /// <example>2024-12-18T14:30:00Z</example>
        public DateTime Timestamp { get; set; }
    }
}
