FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY Gateway/Gateway.csproj Gateway/
COPY Common/Common.csproj Common/
RUN dotnet restore Gateway/Gateway.csproj

COPY Gateway/ ./Gateway/
COPY Common/ ./Common/
WORKDIR /src/Gateway
RUN dotnet build -c Release -o /app/build
RUN dotnet publish -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Gateway.dll"]
