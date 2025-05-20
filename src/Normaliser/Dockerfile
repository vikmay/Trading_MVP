FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY Normaliser/Normaliser.csproj Normaliser/
COPY Common/Common.csproj Common/
RUN dotnet restore Normaliser/Normaliser.csproj

COPY Normaliser/ ./Normaliser/
COPY Common/ ./Common/
WORKDIR /src/Normaliser
RUN dotnet build -c Release -o /app/build
RUN dotnet publish -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Normaliser.dll"]
