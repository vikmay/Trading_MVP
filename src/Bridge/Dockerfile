# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app

# Copy project and restore
COPY ./Bridge/Bridge.csproj ./Bridge/
RUN dotnet restore ./Bridge/Bridge.csproj

# Copy all source and build
COPY ./Bridge ./Bridge
WORKDIR /app/Bridge
RUN dotnet publish -c Release -o /out

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/runtime:8.0
WORKDIR /app
COPY --from=build /out ./

ENTRYPOINT ["dotnet", "Bridge.dll"]
