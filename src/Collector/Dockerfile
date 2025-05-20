FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY Collector/Collector.csproj Collector/
COPY Common/Common.csproj Common/
RUN dotnet restore Collector/Collector.csproj

COPY Collector/ ./Collector/
COPY Common/ ./Common/
WORKDIR /src/Collector
RUN dotnet build -c Release -o /app/build
RUN dotnet publish -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Collector.dll"]
