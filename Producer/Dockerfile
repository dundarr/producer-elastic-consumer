# Build and runtime stage for the Producer service
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 3000

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Clear any NuGet configuration that might reference Windows paths
ENV NUGET_PACKAGES=/root/.nuget/packages

# COPY the project files first to leverage Docker layer cache.
COPY Producer/Producer.csproj Producer/
RUN dotnet restore Producer/Producer.csproj

# Copy the rest of the sources and publish
COPY Producer/ Producer/
WORKDIR /src/Producer

# Publish with restore to ensure clean build
RUN dotnet publish -c Release -o /app

FROM base AS final
WORKDIR /app
COPY --from=build /app .

# Set environment to Docker
ENV DOTNET_ENVIRONMENT=Docker

ENTRYPOINT ["dotnet", "Producer.dll"]
