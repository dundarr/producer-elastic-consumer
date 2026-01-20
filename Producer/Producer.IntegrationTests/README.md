# Integration Tests - Quick Start

## Prerequisites

These integration tests require **Azurite** (Azure Storage Emulator) running locally.

## Quick Start

### 1. Start Azurite (Recommended - All Services)

```bash
docker run --rm --name azurite -p 10000:10000 -p 10001:10001 -p 10002:10002 mcr.microsoft.com/azure-storage/azurite
```

### 2. Verify Azurite is Running

```powershell
# Windows PowerShell
Test-NetConnection -ComputerName 127.0.0.1 -Port 10001
```

Expected: `TcpTestSucceeded : True`

### 3. Run Tests

```bash
# Run diagnostic tests first
dotnet test Producer.IntegrationTests --filter "FullyQualifiedName~AzuriteConnectionTests"

# Run all integration tests
dotnet test Producer.IntegrationTests
```

## Alternative: Queue Service Only

If you only want the Queue service:

```bash
docker run --rm --name azurite -p 10001:10001 mcr.microsoft.com/azure-storage/azurite azurite-queue --queueHost 0.0.0.0 --queuePort 10001
```

**Note**: Tests use an explicit connection string that only requires Queue service.

## Connection Configuration

Tests use this connection string:
```csharp
DefaultEndpointsProtocol=http;
AccountName=devstoreaccount1;
AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;
QueueEndpoint=http://127.0.0.1:10001/devstoreaccount1;
```

This explicitly targets only the Queue endpoint, avoiding issues with `UseDevelopmentStorage=true`.

## Troubleshooting

If you get "Connection refused" errors, see **[AZURITE_TROUBLESHOOTING.md](AZURITE_TROUBLESHOOTING.md)** for detailed diagnosis steps.

### Common Issues Quick Fix

1. **Azurite not running**: Use the docker command above
2. **Wrong port**: Verify with `docker ps` that port 10001 is mapped
3. **Firewall blocking**: Check Windows Firewall settings
4. **Old container running**: Stop with `docker stop <id>` and restart

## Test Details

- **Test Framework**: xUnit with FluentAssertions
- **Setup**: Each test creates a unique queue (IAsyncLifetime)
- **Cleanup**: Queues are automatically deleted after each test
- **Isolation**: Random queue names prevent test interference
