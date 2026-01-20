using Producer.Services;

namespace Producer.UnitTests;

/// <summary>
/// Unit tests for ConsumerRegistry - validates thread-safe consumer tracking.
/// </summary>
public class ConsumerRegistryTests
{
    [Fact]
    public async Task RegisterAsync_ShouldAddNewConsumer()
    {
        // Arrange
        var registry = new ConsumerRegistry();
        var consumerId = Guid.NewGuid();

        // Act
        await registry.RegisterAsync(consumerId);
        var registered = await registry.GetRegisteredAsync();

        // Assert
        registered.Should().ContainKey(consumerId);
    }

    [Fact]
    public async Task RegisterAsync_ShouldUpdateTimestampForExistingConsumer()
    {
        // Arrange
        var registry = new ConsumerRegistry();
        var consumerId = Guid.NewGuid();

        // Act
        await registry.RegisterAsync(consumerId);
        var firstTimestamp = (await registry.GetRegisteredAsync())[consumerId];
        
        await Task.Delay(50); // Ensure time difference
        
        await registry.RegisterAsync(consumerId);
        var secondTimestamp = (await registry.GetRegisteredAsync())[consumerId];

        // Assert
        secondTimestamp.Should().BeAfter(firstTimestamp);
    }

    [Fact]
    public async Task UnregisterAsync_ShouldRemoveConsumer()
    {
        // Arrange
        var registry = new ConsumerRegistry();
        var consumerId = Guid.NewGuid();
        await registry.RegisterAsync(consumerId);

        // Act
        await registry.UnregisterAsync(consumerId);
        var registered = await registry.GetRegisteredAsync();

        // Assert
        registered.Should().NotContainKey(consumerId);
    }

    [Fact]
    public async Task UnregisterAsync_ShouldNotThrow_WhenConsumerDoesNotExist()
    {
        // Arrange
        var registry = new ConsumerRegistry();
        var consumerId = Guid.NewGuid();

        // Act
        var act = async () => await registry.UnregisterAsync(consumerId);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task GetRegisteredAsync_ShouldReturnEmptyDictionary_WhenNoConsumersRegistered()
    {
        // Arrange
        var registry = new ConsumerRegistry();

        // Act
        var registered = await registry.GetRegisteredAsync();

        // Assert
        registered.Should().BeEmpty();
    }

    [Fact]
    public async Task GetRegisteredAsync_ShouldReturnAllRegisteredConsumers()
    {
        // Arrange
        var registry = new ConsumerRegistry();
        var consumer1 = Guid.NewGuid();
        var consumer2 = Guid.NewGuid();
        var consumer3 = Guid.NewGuid();

        await registry.RegisterAsync(consumer1);
        await registry.RegisterAsync(consumer2);
        await registry.RegisterAsync(consumer3);

        // Act
        var registered = await registry.GetRegisteredAsync();

        // Assert
        registered.Should().HaveCount(3);
        registered.Should().ContainKeys(consumer1, consumer2, consumer3);
    }

    [Fact]
    public async Task ConcurrentOperations_ShouldBeThreadSafe()
    {
        // Arrange
        var registry = new ConsumerRegistry();
        var consumerIds = Enumerable.Range(0, 100).Select(_ => Guid.NewGuid()).ToList();
        var tasks = new List<Task>();

        // Act - simulate concurrent registrations and unregistrations
        foreach (var id in consumerIds)
        {
            tasks.Add(Task.Run(async () => await registry.RegisterAsync(id)));
        }

        tasks.AddRange(consumerIds.Take(50).Select(id => 
            Task.Run(async () => await registry.UnregisterAsync(id))));

        await Task.WhenAll(tasks);

        var registered = await registry.GetRegisteredAsync();

        // Assert - should have 50 consumers remaining (100 registered - 50 unregistered)
        registered.Should().HaveCount(50);
    }

    [Fact]
    public async Task GetRegisteredAsync_ShouldReturnImmutableSnapshot()
    {
        // Arrange
        var registry = new ConsumerRegistry();
        var consumerId = Guid.NewGuid();
        await registry.RegisterAsync(consumerId);

        // Act
        var snapshot1 = await registry.GetRegisteredAsync();
        await registry.UnregisterAsync(consumerId);
        var snapshot2 = await registry.GetRegisteredAsync();

        // Assert - snapshots should be independent
        snapshot1.Should().ContainKey(consumerId);
        snapshot2.Should().NotContainKey(consumerId);
    }
}