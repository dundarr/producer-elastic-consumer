using System.Threading.Tasks;

namespace Producer.Services;

public interface IQueueMetricsService
{
    Task<int> GetQueueLengthAsync();
    Task<double> GetQueueSpeedAsync(); // messages per second
}
