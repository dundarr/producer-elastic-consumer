using Azure.Storage.Queues;
using Xunit;
using Xunit.Abstractions;

namespace Producer.IntegrationTests;

/// <summary>
/// Diagnostic test to verify Azurite connectivity.
/// Run this test first to ensure Azurite is properly configured.
/// </summary>
public class AzuriteConnectionTests
{
    private readonly ITestOutputHelper _output;

    public AzuriteConnectionTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task CanConnectToAzurite_WithExplicitConnectionString()
    {
        // Arrange
        var connectionString = 
            "DefaultEndpointsProtocol=http;" +
            "AccountName=devstoreaccount1;" +
            "AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;" +
            "QueueEndpoint=http://127.0.0.1:10001/devstoreaccount1;";

        var queueName = $"diagnostic-queue-{Guid.NewGuid():N}";
        var queueClient = new QueueClient(connectionString, queueName);

        _output.WriteLine($"Attempting to connect to: {queueClient.Uri}");

        try
        {
            // Act - Try to create the queue (this will test connectivity)
            var response = await queueClient.CreateIfNotExistsAsync();
            
            _output.WriteLine($"✓ Successfully connected to Azurite");
            _output.WriteLine($"Queue created: {response != null}");

            // Cleanup
            await queueClient.DeleteIfExistsAsync();
            
            // Assert
            Assert.True(true, "Connection successful");
        }
        catch (Exception ex)
        {
            _output.WriteLine($"✗ Connection failed: {ex.Message}");
            _output.WriteLine($"Exception type: {ex.GetType().Name}");
            
            if (ex.InnerException != null)
            {
                _output.WriteLine($"Inner exception: {ex.InnerException.Message}");
            }

            throw new InvalidOperationException(
                "Cannot connect to Azurite. Please ensure it's running:\n" +
                "  Docker (all services): docker run -p 10000:10000 -p 10001:10001 -p 10002:10002 mcr.microsoft.com/azure-storage/azurite\n" +
                "  Docker (queue only): docker run -p 10001:10001 mcr.microsoft.com/azure-storage/azurite azurite-queue --queueHost 0.0.0.0 --queuePort 10001\n" +
                $"Original error: {ex.Message}",
                ex);
        }
    }

    [Fact]
    public async Task CanConnectToAzurite_WithUseDevelopmentStorage()
    {
        // Arrange
        var connectionString = "UseDevelopmentStorage=true";
        var queueName = $"diagnostic-queue-2-{Guid.NewGuid():N}";
        var queueClient = new QueueClient(connectionString, queueName);

        _output.WriteLine($"Attempting to connect using UseDevelopmentStorage=true");
        _output.WriteLine($"This requires ALL Azurite services (Blob, Queue, Table) to be running");
        _output.WriteLine($"Queue URI: {queueClient.Uri}");

        try
        {
            // Act
            var response = await queueClient.CreateIfNotExistsAsync();
            
            _output.WriteLine($"✓ Successfully connected with UseDevelopmentStorage=true");

            // Cleanup
            await queueClient.DeleteIfExistsAsync();
            
            // Assert
            Assert.True(true, "Connection successful");
        }
        catch (Exception ex)
        {
            _output.WriteLine($"✗ Connection with UseDevelopmentStorage=true failed");
            _output.WriteLine($"This is expected if you're only running azurite-queue (not all services)");
            _output.WriteLine($"Error: {ex.Message}");

            throw new InvalidOperationException(
                "UseDevelopmentStorage=true requires ALL Azurite services running.\n" +
                "Start all services with: docker run -p 10000:10000 -p 10001:10001 -p 10002:10002 mcr.microsoft.com/azure-storage/azurite\n" +
                "Or use explicit connection string (see other test)",
                ex);
        }
    }
}
