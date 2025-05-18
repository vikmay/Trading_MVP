FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Копіюємо проекти для restore
COPY Common/Common.csproj Common/
COPY Collector/Collector.csproj Collector/
RUN dotnet restore Collector/Collector.csproj

# Копіюємо весь код
COPY Common/ Common/
COPY Collector/ Collector/

WORKDIR /src/Collector
RUN dotnet build Collector.csproj -c Release -o /app/build

FROM build AS publish
WORKDIR /src/Collector
RUN dotnet publish Collector.csproj -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Collector.dll"]