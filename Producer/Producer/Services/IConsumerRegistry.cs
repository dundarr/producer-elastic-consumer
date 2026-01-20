using System;
using System.Collections.Frozen;
using System.Threading.Tasks;

namespace Producer.Services
{
    public interface IConsumerRegistry
    {
        Task RegisterAsync(Guid consumerId);
        Task UnregisterAsync(Guid consumerId);
        Task<FrozenDictionary<Guid, DateTime>> GetRegisteredAsync();
    }

}
