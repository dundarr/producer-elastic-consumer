# Solution: Integration Tests Cannot Connect to Azurite

## Problem Identified

Integration tests fail with "Connection refused" on `127.0.0.1:10001` even though Azurite is running in Docker.

### Root Cause

**`UseDevelopmentStorage=true` requires ALL Azurite services to be running** (Blob on 10000, Queue on 10001, Table on 10002). If you only run `azurite-queue`, the Azure SDK attempts to validate the connection with all services and fails.

## Implemented Solution

### Change 1: Use Explicit Connection String

I've modified `QueueMetricsServiceIntegrationTests.cs` to use an explicit connection string that **only specifies the Queue endpoint**:

```csharp
private const string AzuriteQueueConnectionString = 
    "DefaultEndpointsProtocol=http;" +
    "AccountName=devstoreaccount1;" +
    "AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;" +
    "QueueEndpoint=http://127.0.0.1:10001/devstoreaccount1;";
```

This connection string prevents the SDK from attempting to connect to Blob and Table services.

### Change 2: Diagnostic Tests

I've created `AzuriteConnectionTests.cs` with tests that:
- Verify connectivity with explicit connection string
- Verify connectivity with `UseDevelopmentStorage=true`
- Provide clear diagnostic messages

## How to Run Azurite Correctly

### Option A: All Services (Recommended)

If your application uses `UseDevelopmentStorage=true`:

```bash
docker run --rm --name azurite -p 10000:10000 -p 10001:10001 -p 10002:10002 mcr.microsoft.com/azure-storage/azurite
```

### Option B: Queue Service Only

If you only need Queue and use explicit connection string:

```bash
docker run --rm --name azurite -p 10001:10001 mcr.microsoft.com/azure-storage/azurite azurite-queue --queueHost 0.0.0.0 --queuePort 10001
```

## Connectivity Verification

### 1. Verify the container is running

```powershell
docker ps
```

You should see:
```
CONTAINER ID   IMAGE                                    PORTS                    STATUS
xxxxx          mcr.microsoft.com/azure-storage/azurite  0.0.0.0:10001->10001/tcp Up X minutes
```

### 2. Check Azurite logs

```powershell
docker logs <container-id>
```

You should see:
```
Azurite Queue service is starting at http://0.0.0.0:10001
Azurite Queue service is successfully listening at http://0.0.0.0:10001
```

### 3. Verify the port is listening

**Windows PowerShell:**
```powershell
Test-NetConnection -ComputerName 127.0.0.1 -Port 10001
```

Expected output:
```
TcpTestSucceeded : True
```

**Or using netstat:**
```powershell
netstat -ano | findstr :10001
```

### 4. Test with HTTP request

```powershell
Invoke-WebRequest -Uri "http://127.0.0.1:10001/devstoreaccount1/?comp=list" -Method GET
```

If Azurite is working, you'll get a response (even if it's an XML error, it confirms the service is responding).

## Running the Tests

Once Azurite is running correctly:

```bash
# Run diagnostic tests only
dotnet test Producer.IntegrationTests --filter "FullyQualifiedName~AzuriteConnectionTests"

# Run all integration tests
dotnet test Producer.IntegrationTests
```

## Common Troubleshooting

### Error: "TcpTestSucceeded : False"

**Cause**: Azurite is not running or is not on the correct port.

**Solution**:
1. Stop all Azurite containers: `docker ps` and `docker stop <id>`
2. Restart with the correct command (see above)
3. Wait 5-10 seconds for it to start completely
4. Verify again

### Error: "Port already in use"

**Cause**: Another process is using port 10001.

**Solution**:
```powershell
# See which process is using the port
netstat -ano | findstr :10001

# Stop the process or map Azurite to another port
docker run --rm --name azurite -p 10002:10001 mcr.microsoft.com/azure-storage/azurite azurite-queue --queueHost 0.0.0.0 --queuePort 10001
```

Then update the connection string in the tests to use port 10002.

### Docker Desktop not responding

**Solution**:
1. Restart Docker Desktop
2. If you're on Windows with WSL2:
   ```powershell
   wsl --shutdown
   # Wait 10 seconds
   # Restart Docker Desktop
   ```

## Summary of Changes

| File | Change |
|---------|--------|
| `QueueMetricsServiceIntegrationTests.cs` | Replaced `UseDevelopmentStorage=true` with explicit string |
| `AzuriteConnectionTests.cs` | New file with diagnostic tests |
| `README.md` | Updated with detailed instructions |
| `AZURITE_TROUBLESHOOTING.md` | This file (new documentation) |

## Next Steps

1. **Stop all current Azurite containers**
2. **Start Azurite with all services** (Option A above)
3. **Verify connectivity** (steps 1-4 above)
4. **Run diagnostic tests**
5. **Run all integration tests**

If the problem persists after following these steps, provide:
- Output from `docker ps`
- Output from `docker logs <container-id>`
- Output from `Test-NetConnection -ComputerName 127.0.0.1 -Port 10001`
