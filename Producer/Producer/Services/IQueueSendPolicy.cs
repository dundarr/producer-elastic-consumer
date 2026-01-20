using System;
using System.Threading;
using System.Threading.Tasks;

namespace Producer.Services;

public interface IQueueSendPolicy
{
    Task ExecuteAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken);
}