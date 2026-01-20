# CONTRIBUTING.md

## Introduction

This document defines the mandatory rules and practices for contributing to this repository. Its goal is to maintain code quality, consistency, and maintainability in the Consumer project (Worker Service .NET 10, C# 14).

## General principles

- Follow SOLID principles and separation of concerns.
- Write clear and readable code for other developers.
- Automate checks (formatting, static analysis, tests) in CI.

## Style and formatting

- Respect the `/.editorconfig` file in the repository. If it does not exist, create one before opening a PR.
- Use `camelCase` for parameters and private fields with a leading underscore (e.g., `_logger`).
- Keep `nullable` enabled and fix nullability warnings.
- Run `dotnet format` locally before opening a PR.

## Constructor and DI

- For dependency injection in services and `BackgroundService`, we prefer **traditional constructors**:
  - Improves clarity and compatibility across environments and tools.
  - Avoid declaring fields and assigning them from a primary constructor mixed with the same dependency.

## BackgroundService and CancellationToken

- Implement `BackgroundService` for long-running work and always respect the `CancellationToken`.
- Check the token in loops, delays, and HTTP calls.
- In `StopAsync` limit the time spent finalizing using `CancellationTokenSource.CreateLinkedTokenSource` and `CancelAfter` to avoid blocking shutdown.

## Resilience and HTTP communication

- Centralize retry logic in a `DelegatingHandler` (e.g., `HttpRetryHandler`) or at the `HttpClientFactory` level.
- Implement retries with exponential backoff + jitter.
- Do not block threads during retries; use asynchronous APIs and respect `CancellationToken`.
- The project uses `HttpRetryHandler` registered as a `DelegatingHandler` in the typed HTTP client `"ApiClient"`.

## Queues and messaging

- Externalize settings into `IOptions<QueueSettings>` and validate them in `Program.cs`.
- Handle visibility timeout, retries, and dead-letter queues explicitly.
- Avoid business logic inside the worker; delegate to testable services (e.g., `IMessageProcessor`).
- Configuration includes:
  - `ConnectionString`, `Name`, `DeadLetterQueueName`
  - `DeadLetterThreshold`: retry threshold before moving to dead-letter
  - `VisibilityTimeoutSeconds`: message visibility timeout
  - `FixedConsumptionRatePerSecond`: processing rate limit
  - `ReceiveMaxRetries`, `ReceiveBaseDelayMs`: retry configuration for receiving messages

## Configuration

- Use the Options pattern (`IOptions<T>`) for all configurations.
- Validate critical configuration in `Program.cs` with `.Validate()`.
- Current configurations:
  - `QueueSettings`: Azure Storage Queue configuration
  - `ProducerApiSettings`: Producer API configuration

## Current Workers

The project implements two `BackgroundService` classes:

1. **QueueConsumerWorker**: 
   - Consumes messages from Azure Storage Queue with sequential processing
   - Implements configurable rate limiting
   - Handles retries with exponential backoff + jitter
   - Uses `IQueueClient` (testable wrapper) and delegates processing to `IMessageProcessor`

2. **HeartbeatWorker**:
   - Sends periodic heartbeats to the producer API
   - Registers/unregisters the consumer on startup/shutdown
   - Uses `IHttpClientFactory` with the typed client `"ApiClient"`

## Testing

- Add unit tests for critical components (`IMessageProcessor`, `HttpRetryHandler`, `QueueConsumerWorker`, etc.).
- Include integration tests for interactions with Azure Storage Queues when possible (use Azurite emulator or test account).
- Recommended target: >= 70% coverage in critical modules.
- Workers accept constructor overloads to inject testable dependencies (e.g., fake `IQueueClient`).

## Security

- Do not commit secrets or credentials. Use __User Secrets__ in development and environment variables in production.
- Review least-privilege permissions for accounts accessing resources (Storage, APIs).
- Use `UseDevelopmentStorage=true` or Azurite for local development.

## CI / Pipeline

- The pipeline should run at minimum: `dotnet restore`, `dotnet build --no-restore`, `dotnet format --verify-no-changes`, `dotnet test`, and static analysis (Roslyn Analyzers).
- Reject PRs that break formatting or introduce critical warnings.

## Docker

- The project includes a multi-stage `Dockerfile` using .NET 10 SDK and Runtime images.
- `docker-compose.yml` configures the consumer + Azurite for local development.
- All configurations are injected via environment variables in the container.

## Pull Request review

- Provide a clear description of the change, how to test it, and side effects.
- Add tests that cover changes or bug fixes.
- Keep PRs small and focused.

## Commit conventions

- Use clear commit messages. Suggested prefixes: `feat:`, `fix:`, `chore:`, `refactor:`, `test:`.

## Documentation

- Document important architectural decisions in `README.md` or in `docs/`.

## Practical application to this solution

- Avoid primary constructors in `BackgroundService` and `IMessageProcessor` for consistency: switch to traditional constructors.
- Add `/.editorconfig` with team rules before formatting the repo.
- `HttpRetryHandler` implements centralized retries for all HTTP calls.
- Workers follow the recommended pattern: delegate logic to injected services and respect `CancellationToken`.

Thank you for contributing. If you have questions, open an issue describing your proposed change.