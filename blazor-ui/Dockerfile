FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Dashboard/Dashboard/Dashboard.csproj Dashboard/
RUN dotnet restore Dashboard/Dashboard.csproj
COPY Dashboard/Dashboard/ Dashboard/
RUN dotnet publish Dashboard/Dashboard.csproj --no-restore -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "Dashboard.dll"]
