# Contributing Guide

## 🎯 Project Overview

This is a **Producer-Consumer microservices architecture** demonstration project using:
- **.NET 10 Worker Services** (Background services)
- **Azure Queue Storage** (Message broker)
- **Azurite** (Local Azure Storage emulator)
- **ASP.NET Core Web API** (Control endpoints)
- **Docker** (Containerization)

## 📋 Prerequisites

- **.NET 10 SDK**
- **Docker** (for Azurite emulator)
- **Visual Studio 2022** or **VS Code**
- Basic understanding of:
  - Async/await patterns
  - Dependency injection
  - Background services
  - Message queues

## 🚀 Getting Started

### 1. Clone and Setup

```bash
git clone <repository-url>
cd producer-consumer
```

### 2. Start Azurite (Required for Integration Tests)

```bash
docker run --rm --name azurite -p 10000:10000 -p 10001:10001 -p 10002:10002 mcr.microsoft.com/azure-storage/azurite
```

### 3. Run Tests

```bash
# Unit tests (no Azurite required)
dotnet test Producer.UnitTests
dotnet test Consumer.UnitTests

# Integration tests (Azurite required)
dotnet test Producer.IntegrationTests
dotnet test Consumer.IntegrationTests
```

### 4. Run the Services

```bash
# Terminal 1 - Producer
cd Producer
dotnet run

# Terminal 2 - Consumer
cd Consumer
dotnet run
```

## 🧠 Understanding Complex Code Patterns

### 1. Thread-Safe Lazy Queue Initialization (ProducerWorker.cs)

**Location**: `Producer/Workers/ProducerWorker.cs` - `EnsureQueueAsync()` method

**Problem Solved**: Ensure the queue is created exactly once, even when multiple threads try to send messages simultaneously.

**How It Works**:

```csharp
private Task? _ensureTask;

private async Task EnsureQueueAsync(CancellationToken cancellationToken)
{
    var existing = _ensureTask;
    if (existing != null)
    {
        // Queue creation already in progress or completed
        await existing.ConfigureAwait(false);
        return;
    }

    // Create new task
    var newTask = InternalEnsureQueueAsync(cancellationToken);
    
    // Atomically set _ensureTask if still null
    var original = Interlocked.CompareExchange(ref _ensureTask, newTask, null);
    
    if (original == null)
    {
        // We won the race - execute our task
        try
        {
            await newTask.ConfigureAwait(false);
        }
        catch
        {
            // On failure, reset to allow retry
            Interlocked.Exchange(ref _ensureTask, null);
            throw;
        }
    }
    else
    {
        // Another thread won - await their task
        await original.ConfigureAwait(false);
    }
}
```

**Key Concepts**:
- **`Interlocked.CompareExchange`**: Atomic compare-and-swap operation
  - Reads `_ensureTask`
  - If it's `null`, sets it to `newTask`
  - Returns the original value
  - All happens atomically (no race conditions)
  
- **Why This Pattern?**:
  - ✅ Only one thread actually creates the queue
  - ✅ Other threads wait for the first one to complete
  - ✅ On failure, resets to allow retry
  - ✅ No locks needed (lock-free programming)

**Visualization**:
```
Thread 1: Check _ensureTask → null → Start creation → Set _ensureTask
Thread 2: Check _ensureTask → NOT null → Wait for Thread 1's task
Thread 3: Check _ensureTask → NOT null → Wait for Thread 1's task
```

### 2. Custom Retry Policy with Exponential Backoff (QueueSendPolicy.cs)

**Location**: `Producer/Services/QueueSendPolicy.cs`

**Problem Solved**: Handle transient failures when sending messages to Azure Queue Storage.

**How It Works**:

```csharp
public async Task ExecuteAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken)
{
    int attempt = 0;

    while (true)
    {
        attempt++;
        try
        {
            // Create timeout for this attempt (10 seconds)
            using var timeoutCts = new CancellationTokenSource(_timeout);
            
            // Combine cancellation tokens
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken, 
                timeoutCts.Token);
            
            await action(linked.Token).ConfigureAwait(false);
            return; // Success!
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Service stopping - propagate immediately
            throw;
        }
        catch (Exception ex)
        {
            if (attempt > _maxRetries)
            {
                _logger.LogError(ex, "Failed after {Attempt} attempts.", attempt - 1);
                throw;
            }

            // Exponential backoff: 2^(attempt-1) * 2 seconds
            var delaySeconds = Math.Pow(2, attempt - 1) * 2;
            var delay = TimeSpan.FromSeconds(delaySeconds);
            
            _logger.LogWarning(ex, "Attempt {Attempt} failed. Retrying after {Delay}.", attempt, delay);
            
            await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
        }
    }
}
```

**Retry Schedule**:
- Attempt 1: Immediate → Fails → Wait 2s
- Attempt 2: After 2s → Fails → Wait 4s
- Attempt 3: After 4s → Fails → Wait 8s
- Attempt 4: After 8s → Fails → Give up, throw exception

**Key Concepts**:
- **Exponential Backoff**: `2^(attempt-1) * baseDelay`
  - Reduces load on failing service
  - Gives time for transient issues to resolve
  
- **Linked CancellationTokens**:
  - `cancellationToken`: Service stopping
  - `timeoutCts.Token`: Single attempt timeout
  - Both can cancel the operation

- **Graceful Shutdown**: Distinguishes between timeout and service stopping

### 3. Thread-Safe Rate Control (ProducerWorkerControl.cs)

**Location**: `Producer/Services/ProducerWorkerControl.cs`

**Problem Solved**: Allow runtime changes to message production rate from multiple threads safely.

**How It Works**:

```csharp
private volatile bool _running;
private volatile int _messagesPerSecond = 1;

public ProducerWorkerControl(IOptionsMonitor<QueueOptions> options)
{
    var initial = options?.CurrentValue?.MessagesPerSecond ?? 1;
    
    // Atomic update
    Interlocked.Exchange(ref _messagesPerSecond, Math.Max(1, initial));

    // Hot-reload: Configuration changes without restart
    options?.OnChange(o =>
    {
        Interlocked.Exchange(ref _messagesPerSecond, Math.Max(1, o.MessagesPerSecond));
    });
}

public void SetRate(int rate) => 
    Interlocked.Exchange(ref _messagesPerSecond, Math.Max(1, rate));

public int GetRate() => _messagesPerSecond;
```

**Key Concepts**:
- **`volatile`**: Ensures visibility across threads
  - Prevents compiler/CPU from caching the value
  - All threads see the latest value
  
- **`Interlocked.Exchange`**: Atomic write operation
  - Writes new value
  - Returns old value
  - Thread-safe without locks
  
- **`IOptionsMonitor.OnChange`**: Hot-reload configuration
  - Detects changes to `appsettings.json`
  - Updates rate without restarting service

**Why Not Use Locks?**
- ✅ Better performance (no contention)
- ✅ No risk of deadlocks
- ✅ Simple read/write operations don't need locks

### 4. BackgroundService Pattern (ProducerWorker.cs)

**Location**: `Producer/Workers/ProducerWorker.cs` - `ExecuteAsync()` method

**Problem Solved**: Long-running background task that respects graceful shutdown.

**How It Works**:

```csharp
protected override async Task ExecuteAsync(CancellationToken stoppingToken)
{
    _logger.LogInformation("ProducerWorker started (in paused mode).");

    while (!stoppingToken.IsCancellationRequested)
    {
        bool running = _control.IsRunning();
        int rate = _control.GetRate();

        if (!running)
        {
            // Paused - check every 250ms
            await Task.Delay(250, stoppingToken);
            continue;
        }

        // Send 'rate' messages
        for (int i = 0; i < rate && !stoppingToken.IsCancellationRequested; i++)
        {
            var id = Guid.NewGuid();
            try
            {
                await EnsureQueueAsync(stoppingToken).ConfigureAwait(false);
                
                await _sendPolicy.ExecuteAsync(async ct =>
                {
                    var message = JsonSerializer.Serialize($"msg-{id}");
                    await _queueClient.SendMessageAsync(message, cancellationToken: ct);
                }, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Graceful shutdown
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending message {MessageId}.", id);
            }
        }

        // Wait 1 second before next batch (implements "messages per second")
        await Task.Delay(1000, stoppingToken);
    }

    _logger.LogInformation("ProducerWorker stopped.");
}
```

**Key Concepts**:
- **`CancellationToken`**: Signals graceful shutdown
  - Check `stoppingToken.IsCancellationRequested` frequently
  - Pass to all async operations
  
- **Batch Processing**: Sends `rate` messages per second
  - Loop sends `rate` messages
  - Wait 1 second
  - Repeat

- **Error Handling**: Don't crash the service
  - Catch exceptions per message
  - Log and continue
  - Only stop on cancellation

### 5. Azure Queue Name Validation (ProducerWorker.cs)

**Location**: `Producer/Workers/ProducerWorker.cs` - `ValidateQueueName()` method

**Problem Solved**: Detect invalid queue names early with clear error messages.

**Azure Queue Naming Rules**:
- ✅ Lowercase letters (a-z)
- ✅ Numbers (0-9)
- ✅ Hyphens (-)
- ✅ 3-63 characters
- ✅ Must start/end with letter or number
- ❌ NO uppercase
- ❌ NO underscores
- ❌ NO consecutive hyphens

**Regex Pattern**:
```csharp
var validPattern = @"^[a-z0-9]([a-z0-9]|-(?!-))*[a-z0-9]$|^[a-z0-9]$";
```

**Breakdown**:
- `^[a-z0-9]`: Start with letter or number
- `([a-z0-9]|-(?!-))*`: Middle can be letter, number, or hyphen (not consecutive)
  - `-(?!-)`: Hyphen NOT followed by another hyphen (negative lookahead)
- `[a-z0-9]$`: End with letter or number
- `|^[a-z0-9]$`: OR single character (edge case)

**Example Error Message**:
```
Queue name 'MyQueue_Name' is invalid. Issues found:
  - contains uppercase letters (only lowercase allowed)
  - contains underscores (only hyphens allowed)
Valid names: lowercase letters, numbers, hyphens only; 3-63 chars; start/end with letter or number.
```

### 6. Integration Test Setup (IAsyncLifetime)

**Location**: `Producer.IntegrationTests/QueueMetricsServiceIntegrationTests.cs`

**Problem Solved**: Each test needs a fresh, isolated queue.

**How It Works**:

```csharp
public class QueueMetricsServiceIntegrationTests : IAsyncLifetime
{
    private readonly QueueClient _queueClient;
    private readonly string _queueName;

    public QueueMetricsServiceIntegrationTests()
    {
        // Unique queue name per test run
        _queueName = $"test-metrics-queue-{Guid.NewGuid():N}";
        _queueClient = new QueueClient(connectionString, _queueName);
    }

    // Called BEFORE each test
    public async Task InitializeAsync()
    {
        await _queueClient.CreateIfNotExistsAsync();
    }

    // Called AFTER each test
    public async Task DisposeAsync()
    {
        await _queueClient.DeleteIfExistsAsync();
    }

    [Fact]
    public async Task MyTest()
    {
        // Test has fresh queue created by InitializeAsync
        // Will be cleaned up by DisposeAsync
    }
}
```

**Key Concepts**:
- **`IAsyncLifetime`**: xUnit interface for async setup/cleanup
- **Unique Queue Names**: `Guid.NewGuid():N` prevents test interference
- **Automatic Cleanup**: Ensures no leftover test queues

### 7. Queue Auto-Creation in Services (QueueMetricsService.cs)

**Location**: `Producer/Services/QueueMetricsService.cs`

**Problem Solved**: Metrics endpoints (`/metrics/length`, `/metrics/speed`) should not fail if the queue doesn't exist yet. They should create it automatically, just like the producer does.

**How It Works**:

```csharp
private Task? _ensureQueueTask;
private readonly object _lock = new object();

private async Task EnsureQueueExistsAsync()
{
    if (_ensureQueueTask != null)
    {
        await _ensureQueueTask.ConfigureAwait(false);
        return;
    }

    lock (_lock)
    {
        // Double-checked locking pattern
        if (_ensureQueueTask == null)
        {
            _ensureQueueTask = CreateQueueIfNotExistsAsync();
        }
    }

    await _ensureQueueTask.ConfigureAwait(false);
}

private async Task CreateQueueIfNotExistsAsync()
{
    try
    {
        await _queueClient.CreateIfNotExistsAsync().ConfigureAwait(false);
        _logger.LogInformation("Queue '{QueueName}' is ready for metrics.", _queueClient.Name);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Failed to create or verify queue '{QueueName}'.", _queueClient.Name);
        
        // Reset to allow retry
        lock (_lock)
        {
            _ensureQueueTask = null;
        }
        
        throw;
    }
}

public async Task<int> GetQueueLengthAsync()
{
    // Ensure queue exists before reading
    await EnsureQueueExistsAsync().ConfigureAwait(false);
    
    var props = await _queueClient.GetPropertiesAsync().ConfigureAwait(false);
    return props.Value.ApproximateMessagesCount;
}
```

**Key Concepts**:
- **Double-Checked Locking**: Check → Lock → Check again → Create
  - First check avoids lock contention after initialization
  - Lock ensures only one thread creates the task
  - Second check inside lock handles race conditions
  
- **Lazy Initialization**: Queue creation deferred until first use
  - Service can be constructed without Azurite running
  - Queue created on first metrics request
  
- **Retry on Failure**: Task reset on error
  - If creation fails, next call will retry
  - Resilient to transient failures

**Comparison with ProducerWorker**:

| Feature | ProducerWorker | QueueMetricsService |
|---------|----------------|---------------------|
| Pattern | `Interlocked.CompareExchange` | `lock` statement |
| Thread-safe | ✅ Yes | ✅ Yes |
| Retry on failure | ✅ Yes | ✅ Yes |
| Performance | Slightly faster (lock-free) | Simpler code |
| Use case | High-frequency (message sends) | Lower-frequency (metrics) |

**Why Different Patterns?**:
- `ProducerWorker`: Lock-free for maximum throughput (sends many messages/sec)
- `QueueMetricsService`: Simpler lock-based (metrics called less frequently)

Both are correct and thread-safe. Choose based on:
- **Lock-free** (`Interlocked`): When performance is critical
- **Lock-based**: When simplicity/readability is preferred

See: `Producer/QUEUE_METRICS_AUTO_CREATE.md` for detailed documentation.

## 🔍 Common Pitfalls & Solutions

### Pitfall 1: Queue Name with Uppercase/Underscores

**Problem**:
```json
{
  "Queue": {
    "QueueName": "MyQueue_Name"  // ❌ Invalid!
  }
}
```

**Solution**:
```json
{
  "Queue": {
    "QueueName": "my-queue-name"  // ✅ Valid
  }
}
```

**Why**: Azure Queue Storage only accepts lowercase, numbers, and hyphens.

See: `Producer.IntegrationTests/QUEUE_NAME_ERROR_400.md`

### Pitfall 2: Forgetting to Start Azurite

**Problem**: Integration tests fail with "Connection refused"

**Solution**:
```bash
docker run --rm -p 10001:10001 mcr.microsoft.com/azure-storage/azurite
```

**Why**: Integration tests need Azurite running on port 10001.

See: `Producer.IntegrationTests/AZURITE_TROUBLESHOOTING.md`

### Pitfall 3: Not Passing CancellationToken

**Problem**:
```csharp
await SomeLongOperationAsync();  // ❌ Won't cancel on shutdown
```

**Solution**:
```csharp
await SomeLongOperationAsync(stoppingToken);  // ✅ Respects cancellation
```

**Why**: Background services need to stop gracefully when the application shuts down.

### Pitfall 4: Blocking in ExecuteAsync

**Problem**:
```csharp
protected override async Task ExecuteAsync(CancellationToken stoppingToken)
{
    Thread.Sleep(1000);  // ❌ Blocks thread pool thread
}
```

**Solution**:
```csharp
protected override async Task ExecuteAsync(CancellationToken stoppingToken)
{
    await Task.Delay(1000, stoppingToken);  // ✅ Non-blocking
}
```

**Why**: `ExecuteAsync` should be truly asynchronous to avoid thread pool starvation.

## 📝 Code Style Guidelines

### 1. Async Methods

```csharp
// ✅ Good
public async Task<int> GetCountAsync(CancellationToken cancellationToken)
{
    await Task.Delay(100, cancellationToken).ConfigureAwait(false);
    return 42;
}

// ❌ Bad
public async Task<int> GetCount()  // Missing CancellationToken
{
    await Task.Delay(100);  // No ConfigureAwait
    return 42;
}
```

**Rules**:
- Always suffix with `Async`
- Accept `CancellationToken` parameter
- Use `.ConfigureAwait(false)` in library code

### 2. Dependency Injection

```csharp
// ✅ Good - Interface-based
public class ProducerWorker : BackgroundService
{
    private readonly IProducerWorkerControl _control;
    
    public ProducerWorker(IProducerWorkerControl control)
    {
        _control = control;
    }
}

// ❌ Bad - Concrete class
public class ProducerWorker : BackgroundService
{
    private readonly ProducerWorkerControl _control;
    
    public ProducerWorker(ProducerWorkerControl control)
    {
        _control = control;
    }
}
```

**Rules**:
- Depend on interfaces, not implementations
- Use constructor injection
- Never use `new` for services

### 3. Logging

```csharp
// ✅ Good - Structured logging
_logger.LogInformation("Sending message {MessageId} at rate {Rate}", id, rate);

// ❌ Bad - String interpolation
_logger.LogInformation($"Sending message {id} at rate {rate}");
```

**Rules**:
- Use structured logging (placeholders)
- Log important state changes
- Use appropriate log levels

### 4. Tests

```csharp
// ✅ Good - Arrange-Act-Assert
[Fact]
public void SetRate_ShouldUpdateRate()
{
    // Arrange
    var control = new ProducerWorkerControl(null!);

    // Act
    control.SetRate(25);

    // Assert
    control.GetRate().Should().Be(25);
}

// ❌ Bad - No structure
[Fact]
public void Test1()
{
    var control = new ProducerWorkerControl(null!);
    control.SetRate(25);
    Assert.Equal(25, control.GetRate());
}
```

**Rules**:
- Use Arrange-Act-Assert pattern
- Descriptive test names: `MethodName_Scenario_ExpectedBehavior`
- Use FluentAssertions for readability

## 🧪 Testing Strategy

### Unit Tests
- **No external dependencies** (no Azurite, no network)
- **Mock dependencies** using Moq
- **Fast** (< 1 second per test)
- **Test logic, not infrastructure**

Example: `Producer.UnitTests/ProducerWorkerControlTests.cs`

### Integration Tests
- **Real Azurite instance** required
- **Test actual Azure SDK behavior**
- **Slower** (network operations)
- **Test end-to-end scenarios**

Example: `Producer.IntegrationTests/QueueMetricsServiceIntegrationTests.cs`

### When to Write Each:

| Scenario | Unit Test | Integration Test |
|----------|-----------|------------------|
| Business logic | ✅ | |
| Control flow | ✅ | |
| Error handling | ✅ | |
| Queue creation | | ✅ |
| Message sending | | ✅ |
| Azure SDK behavior | | ✅ |

## 🐛 Debugging Tips

### 1. Enable Detailed Logging

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "Microsoft": "Information",
      "Azure.Core": "Debug"
    }
  }
}
```

### 2. Use Tracepoints (Non-breaking Breakpoints)

In Visual Studio, right-click a breakpoint → Actions → Log message:
```
Rate changed to {rate}, current messages per second: {_messagesPerSecond}
```

### 3. Azurite Logs

```bash
docker logs <azurite-container-id>
```

### 4. Queue Inspection

Use **Azure Storage Explorer** or PowerShell:
```powershell
# Install Azure.Storage.Queues
Install-Package Azure.Storage.Queues

# List queues
$client = New-Object Azure.Storage.Queues.QueueServiceClient("UseDevelopmentStorage=true")
$queues = $client.GetQueues()
$queues | Format-Table Name
```

## 📚 Additional Resources

- [Azure Queue Storage Documentation](https://docs.microsoft.com/azure/storage/queues/)
- [.NET Background Services](https://docs.microsoft.com/aspnet/core/fundamentals/host/hosted-services)
- [Azurite Documentation](https://docs.microsoft.com/azure/storage/common/storage-use-azurite)
- [xUnit Documentation](https://xunit.net/docs/getting-started/netcore/cmdline)
- [FluentAssertions](https://fluentassertions.com/introduction)

## 🤝 How to Contribute

1. **Fork the repository**
2. **Create a feature branch**: `git checkout -b feature/my-feature`
3. **Write tests** for your changes
4. **Ensure all tests pass**: `dotnet test`
5. **Follow code style guidelines** (see above)
6. **Update documentation** if needed
7. **Submit a pull request**

## 📞 Questions?

- Open an issue on GitHub
- Check existing documentation in `*.md` files
- Review related test files for examples

---

**Thank you for contributing!** 🎉
