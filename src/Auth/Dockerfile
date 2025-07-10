# Use the ASP.NET Core runtime as base image
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

# Use the SDK image to build the application
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project files
COPY ["Auth/Auth.csproj", "Auth/"]
COPY ["Common/Common.csproj", "Common/"]

# Restore dependencies
RUN dotnet restore "Auth/Auth.csproj"

# Copy source code
COPY ["Auth/", "Auth/"]
COPY ["Common/", "Common/"]

# Build the application
WORKDIR "/src/Auth"
RUN dotnet build "Auth.csproj" -c Release -o /app/build

# Publish the application
RUN dotnet publish "Auth.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Final stage/image
FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Auth.dll"]
