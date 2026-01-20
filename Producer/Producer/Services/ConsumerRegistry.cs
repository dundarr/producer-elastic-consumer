using System;
using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Threading.Tasks;

namespace Producer.Services;

/// <summary>
/// Thread-safe registry for tracking active consumers with their last heartbeat timestamp.
/// Uses ConcurrentDictionary for lock-free thread-safe operations.
/// </summary>
public class ConsumerRegistry : IConsumerRegistry
{
    // Thread-safe dictionary: Key = ConsumerId, Value = Last heartbeat timestamp
    private readonly ConcurrentDictionary<Guid, DateTime> _consumers = new();

    /// <summary>
    /// Registers or updates a consumer's heartbeat timestamp.
    /// If consumer exists, updates their timestamp; otherwise adds new entry.
    /// </summary>
    public Task RegisterAsync(Guid consumerId)
    {
        // Indexer assignment is atomic in ConcurrentDictionary
        _consumers[consumerId] = DateTime.Now;
        return Task.CompletedTask;
    }

    /// <summary>
    /// Removes a consumer from the registry.
    /// Safe to call even if consumer doesn't exist.
    /// </summary>
    public Task UnregisterAsync(Guid consumerId)
    {
        // TryRemove is thread-safe and won't throw if key doesn't exist
        _consumers.TryRemove(consumerId, out _);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Returns a snapshot of all registered consumers as an immutable FrozenDictionary.
    /// Safe for enumeration without locking - creates a point-in-time copy.
    /// </summary>
    public Task<FrozenDictionary<Guid, DateTime>> GetRegisteredAsync()
    {
        // ToFrozenDictionary creates an immutable snapshot of the current state
        return Task.FromResult(_consumers.ToFrozenDictionary<Guid, DateTime>());
    }
}
