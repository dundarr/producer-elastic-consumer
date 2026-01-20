# Build stage (use .NET 10 SDK to match project target)
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Clear any NuGet configuration that might reference Windows paths
ENV NUGET_PACKAGES=/root/.nuget/packages

# COPY the project files first to leverage Docker layer cache.
COPY Consumer/Consumer.csproj Consumer/
RUN dotnet restore Consumer/Consumer.csproj

# Copy the rest of the sources and publish
COPY Consumer/ Consumer/
WORKDIR /src/Consumer

# Publish with restore to ensure clean build
RUN dotnet publish -c Release -o /app

# Runtime stage (use .NET 10 runtime)
FROM mcr.microsoft.com/dotnet/runtime:10.0
WORKDIR /app

# Set environment to use Docker-specific configuration
# Use DOTNET_ENVIRONMENT for Worker Services
ENV DOTNET_ENVIRONMENT=Docker

COPY --from=build /app .
ENTRYPOINT ["dotnet", "Consumer.dll"]
