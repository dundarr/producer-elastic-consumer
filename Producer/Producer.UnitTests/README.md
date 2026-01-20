# Producer.UnitTests

## Test Coverage

### Unit Tests (37 + 20 = 57 tests)

#### ProducerWorkerControlTests (12 tests)
- Thread-safety validation
- Rate configuration and limits
- State management
- Configuration hot-reload

#### ConsumerRegistryTests (8 tests)
- Consumer registration/unregistration
- Thread-safe operations
- Immutable snapshots

#### QueueSendPolicyTests (10 tests)
- Retry logic with exponential backoff
- Timeout handling
- Cancellation token support
- Logging verification

#### ProducerControllerTests (7 tests)
- REST API endpoints
- DTO validation
- State management

#### ProducerWorkerTests (7 tests)
- Message production logic
- Rate limiting
- Exception handling
- Queue initialization

#### ConsumerCleanupWorkerTests (8 tests)
- Stale consumer detection
- Periodic cleanup
- Exception handling

#### QueueMetricsServiceTests (3 tests)
- Configuration validation
- Basic instantiation

### Integration Tests (2 tests - skipped by default)

#### QueueMetricsServiceIntegrationTests
- Requires Azurite or Azure Storage Emulator
- Queue length calculation
- Speed calculation

## Running Tests

### Run all unit tests