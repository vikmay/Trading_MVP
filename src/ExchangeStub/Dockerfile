FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Копіюємо csproj для restore
COPY Common/Common.csproj Common/
COPY ExchangeStub/ExchangeStub.csproj ExchangeStub/
RUN dotnet restore ExchangeStub/ExchangeStub.csproj

# Копіюємо весь код
COPY Common/ Common/
COPY ExchangeStub/ ExchangeStub/

WORKDIR /src/ExchangeStub
RUN dotnet build ExchangeStub.csproj -c Release -o /app/build

FROM build AS publish
WORKDIR /src/ExchangeStub
RUN dotnet publish ExchangeStub.csproj -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "ExchangeStub.dll"]